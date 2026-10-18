using System.ComponentModel;
using System.Security.Cryptography;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

public enum SituacaoDoVale
{
    Ativo,
    Usado,
    Vencido,
    Cancelado,
}

/// <summary>
/// Crédito do cliente gerado na troca (obrigação da loja, não receita). Tem código único para apresentar no caixa, validade
/// e pode ser usado em partes como forma de pagamento.
/// </summary>
[DisplayName("Vale-troca")]
public sealed class ValeTroca : ITenantOwned
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem 0/O e 1/I

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = "";
    public Guid? ClienteId { get; private set; }
    public string? ClienteNome { get; private set; }
    public decimal Valor { get; private set; }
    public decimal Saldo { get; private set; }
    public DateOnly EmitidoEm { get; private set; }
    public DateOnly ValidoAte { get; private set; }
    public Guid? TrocaId { get; private set; }
    public bool Cancelado { get; private set; }
    public string? MotivoDoCancelamento { get; private set; }
    public string? CanceladoPor { get; private set; }

    private ValeTroca() { }

    public static string NovoCodigo() => "VT" + new string(Enumerable.Range(0, 6).Select(_ => Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]).ToArray());

    public static ValeTroca Emitir(Guid empresaId, string codigo, Guid? clienteId, string? clienteNome, decimal valor, DateOnly emitidoEm, int validadeDias, Guid? trocaId)
    {
        if (valor <= 0) throw new ArgumentException("O vale-troca precisa ter valor.", nameof(valor));
        return new ValeTroca
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            Codigo = codigo,
            ClienteId = clienteId,
            ClienteNome = clienteNome,
            Valor = Dinheiro.Arredondar(valor),
            Saldo = Dinheiro.Arredondar(valor),
            EmitidoEm = emitidoEm,
            ValidoAte = emitidoEm.AddDays(validadeDias),
            TrocaId = trocaId,
        };
    }

    public SituacaoDoVale Situacao(DateOnly hoje) =>
        Cancelado ? SituacaoDoVale.Cancelado : Saldo <= 0 ? SituacaoDoVale.Usado : hoje > ValidoAte ? SituacaoDoVale.Vencido : SituacaoDoVale.Ativo;

    public void Usar(decimal valor, DateOnly hoje)
    {
        switch (Situacao(hoje))
        {
            case SituacaoDoVale.Cancelado: throw new InvalidOperationException($"O vale {Codigo} foi cancelado.");
            case SituacaoDoVale.Vencido: throw new InvalidOperationException($"O vale {Codigo} venceu em {ValidoAte:dd/MM/yyyy}.");
            case SituacaoDoVale.Usado: throw new InvalidOperationException($"O vale {Codigo} já foi usado.");
        }
        if (valor <= 0) throw new ArgumentException("Informe o valor a usar do vale.", nameof(valor));
        if (valor > Saldo) throw new InvalidOperationException($"O vale {Codigo} tem saldo de R$ {Dinheiro.Formatar(Saldo)}.");
        Saldo -= valor;
    }

    /// <summary>Venda paga com o vale foi cancelada: o valor volta para o vale.</summary>
    public void Devolver(decimal valor) => Saldo = Math.Min(Valor, Saldo + valor);

    public void Cancelar(string motivo, string? usuario)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("Informe o motivo do cancelamento.", nameof(motivo));
        if (Cancelado) throw new InvalidOperationException("Este vale já foi cancelado.");
        (Cancelado, MotivoDoCancelamento, CanceladoPor) = (true, Venda.Limitar(motivo, 300), usuario);
    }
}
