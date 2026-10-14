using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;

namespace Kamus.Modules.Licenciamento.Dominio;

public enum EstadoLicenca
{
    Trial,
    Ativa,
    Carencia,
    Suspensa,
    Cancelada,
}

/// <summary>
/// Licença de uso de um cliente (tenant). O estado é <b>calculado pelas datas</b> — não precisa de rotina para
/// "virar" de Ativa para Carência ou Suspensa, e o pagamento libera na hora.
/// Não é <c>ITenantOwned</c>: o Painel Administrativo lê as licenças de todos os clientes (o filtro por tenant
/// do lado do cliente é explícito). Alterações ficam no registro de ações administrativas.
/// </summary>
[DisplayName("Licença")]
[NaoAuditar]
public sealed class Licenca
{
    public const string PlanoPiloto = "Piloto";
    public const int DiasDeTrialPadrao = 14;
    public const int DiasDeCarenciaPadrao = 5;

    public Guid TenantId { get; private set; }
    public string Plano { get; private set; } = PlanoPiloto;
    public decimal ValorMensal { get; private set; }
    public int DiasDeCarencia { get; private set; } = DiasDeCarenciaPadrao;
    public DateOnly? TrialAte { get; private set; }
    public DateOnly? PagoAte { get; private set; }
    public DateTimeOffset? CanceladaEm { get; private set; }
    public int? LimiteDeCnpjs { get; private set; }
    public int? LimiteDeLojas { get; private set; }
    public int? LimiteDeUsuarios { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Último aviso de vencimento enviado (evita avisar duas vezes o mesmo vencimento).</summary>
    public string? UltimoAviso { get; private set; }

    private Licenca() { }

    public static Licenca CriarTrial(Guid tenantId, DateOnly hoje, DateTimeOffset agora, decimal valorMensal = 199.90m, int diasDeTrial = DiasDeTrialPadrao) =>
        new() { TenantId = tenantId, TrialAte = hoje.AddDays(diasDeTrial), ValorMensal = valorMensal, CriadaEm = agora };

    /// <summary>Até quando o cliente está coberto (pagamento ou, antes do primeiro pagamento, o trial).</summary>
    public DateOnly? Vencimento => PagoAte ?? TrialAte;

    public DateOnly? FimDaCarencia => Vencimento?.AddDays(DiasDeCarencia);

    public EstadoLicenca EstadoEm(DateOnly hoje)
    {
        if (CanceladaEm is not null) return EstadoLicenca.Cancelada;
        if (Vencimento is not { } vencimento) return EstadoLicenca.Suspensa;
        if (hoje <= vencimento) return PagoAte is null ? EstadoLicenca.Trial : EstadoLicenca.Ativa;
        if (hoje <= FimDaCarencia) return EstadoLicenca.Carencia;
        return EstadoLicenca.Suspensa;
    }

    public void RegistrarPagamento(DateOnly novoPagoAte) => PagoAte = novoPagoAte;

    /// <summary>
    /// Registra uma mensalidade e libera na hora. Em dia ou na carência, os meses contam a partir do vencimento
    /// (o cliente não perde dias nem ganha de graça); suspenso, recomeça a partir da data do pagamento.
    /// </summary>
    public Pagamento Pagar(decimal valor, int meses, DateOnly pagoEm, string? forma, string? observacao, string registradoPor, DateTimeOffset agora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);
        ArgumentOutOfRangeException.ThrowIfLessThan(meses, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(meses, 12);
        if (CanceladaEm is not null)
            throw new InvalidOperationException("Licença cancelada: reative antes de registrar pagamento.");

        var inicio = EstadoEm(pagoEm) == EstadoLicenca.Suspensa || Vencimento is null ? pagoEm : Vencimento.Value;
        PagoAte = inicio.AddMonths(meses);
        return Pagamento.Criar(TenantId, valor, meses, pagoEm, PagoAte.Value, forma, observacao, registradoPor, agora);
    }

    public void Cancelar(DateTimeOffset agora) => CanceladaEm ??= agora;

    public void Reativar() => CanceladaEm = null;

    public void DefinirLimites(int? cnpjs, int? lojas, int? usuarios)
    {
        foreach (var limite in new[] { cnpjs, lojas, usuarios })
            if (limite is <= 0) throw new ArgumentOutOfRangeException(nameof(cnpjs), "Limite deve ser maior que zero (ou vazio para sem limite).");
        (LimiteDeCnpjs, LimiteDeLojas, LimiteDeUsuarios) = (cnpjs, lojas, usuarios);
    }

    public void DefinirCarencia(int dias)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dias);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dias, 60);
        DiasDeCarencia = dias;
    }

    public void DefinirValorMensal(decimal valor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(valor);
        ValorMensal = valor;
    }

    /// <returns>Verdadeiro se o aviso ainda não tinha sido enviado.</returns>
    public bool MarcarAviso(string chave)
    {
        if (UltimoAviso == chave) return false;
        UltimoAviso = chave;
        return true;
    }
}
