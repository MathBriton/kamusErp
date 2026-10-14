using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;

namespace Kamus.Modules.Licenciamento.Dominio;

/// <summary>Mensalidade recebida e registrada pelo operador do SaaS no Painel Administrativo.</summary>
[DisplayName("Pagamento da licença")]
[NaoAuditar]
public sealed class Pagamento
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Valor { get; private set; }
    public int Meses { get; private set; }
    public DateOnly PagoEm { get; private set; }
    public DateOnly EstendeuAte { get; private set; }
    public string? Forma { get; private set; }
    public string? Observacao { get; private set; }
    public string RegistradoPor { get; private set; } = "";
    public DateTimeOffset RegistradoEm { get; private set; }

    private Pagamento() { }

    internal static Pagamento Criar(Guid tenantId, decimal valor, int meses, DateOnly pagoEm, DateOnly estendeuAte,
        string? forma, string? observacao, string registradoPor, DateTimeOffset agora) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Valor = valor,
            Meses = meses,
            PagoEm = pagoEm,
            EstendeuAte = estendeuAte,
            Forma = string.IsNullOrWhiteSpace(forma) ? null : forma.Trim(),
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            RegistradoPor = registradoPor,
            RegistradoEm = agora,
        };
}
