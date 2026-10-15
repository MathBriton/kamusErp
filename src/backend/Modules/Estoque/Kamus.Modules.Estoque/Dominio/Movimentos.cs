using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Estoque.Dominio;

public enum TipoDeMovimento
{
    Entrada,
    Ajuste,
    TransferenciaSaida,
    TransferenciaEntrada,
    EstoqueInicial,
    Inventario,
}

/// <summary>Dados comuns aos itens de uma mesma operação (uma entrada com vários SKUs, uma transferência...).</summary>
/// <param name="LojaRelacionadaId">Na transferência: a outra loja (destino na saída, origem na entrada).</param>
public sealed record Operacao(
    Guid Id, DateTimeOffset OcorridoEm, Guid? UsuarioId, string? UsuarioNome, string? Motivo, string? Documento, string? Observacao, Guid? LojaRelacionadaId)
{
    // Compartilhado entre as cópias feitas com "with" (ex.: saída e entrada da mesma transferência).
    private readonly int[] _sequencia = [0];

    /// <summary>Ordem dos movimentos dentro da operação (todos têm o mesmo horário).</summary>
    internal int ProximaSequencia() => _sequencia[0]++;
}

public sealed class EstoqueInsuficienteException(int disponivel, int pedido)
    : InvalidOperationException($"Saldo insuficiente: há {disponivel} e a operação pede {pedido}.")
{
    public int Disponivel { get; } = disponivel;
    public int Pedido { get; } = pedido;
}

/// <summary>
/// Linha do kardex: imutável, nunca editada nem apagada. O saldo é a soma delas; <see cref="SaldoApos"/> e
/// <see cref="CustoMedioApos"/> guardam a foto logo depois do movimento. É o próprio histórico (sem auditoria extra).
/// </summary>
[DisplayName("Movimento de estoque"), NaoAuditar]
public sealed class MovimentoDeEstoque : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperacaoId { get; private set; }

    /// <summary>Ordem dentro da operação (desempata movimentos do mesmo horário no kardex).</summary>
    public int Sequencia { get; private set; }
    public Guid LojaId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public TipoDeMovimento Tipo { get; private set; }

    /// <summary>Positiva na entrada, negativa na saída.</summary>
    public int Quantidade { get; private set; }

    public decimal CustoUnitario { get; private set; }
    public int SaldoApos { get; private set; }
    public decimal CustoMedioApos { get; private set; }
    public string? Motivo { get; private set; }
    public string? Documento { get; private set; }
    public string? Observacao { get; private set; }
    public Guid? LojaRelacionadaId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? UsuarioNome { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }

    private MovimentoDeEstoque() { }

    internal static MovimentoDeEstoque De(SaldoDeEstoque saldo, TipoDeMovimento tipo, int quantidade, decimal custoUnitario, Operacao op) => new()
    {
        Id = Guid.NewGuid(),
        OperacaoId = op.Id,
        Sequencia = op.ProximaSequencia(),
        LojaId = saldo.LojaId,
        SkuId = saldo.SkuId,
        ProdutoId = saldo.ProdutoId,
        Tipo = tipo,
        Quantidade = quantidade,
        CustoUnitario = custoUnitario,
        SaldoApos = saldo.Quantidade,
        CustoMedioApos = saldo.CustoMedio,
        Motivo = op.Motivo,
        Documento = op.Documento,
        Observacao = op.Observacao,
        LojaRelacionadaId = op.LojaRelacionadaId,
        UsuarioId = op.UsuarioId,
        UsuarioNome = op.UsuarioNome,
        OcorridoEm = op.OcorridoEm,
    };
}
