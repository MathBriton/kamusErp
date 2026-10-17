using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Estoque.Dominio;

/// <summary>
/// Saldo e custo médio de um SKU numa loja. Nunca é editado direto: só muda pelos métodos abaixo, que devolvem o
/// <see cref="MovimentoDeEstoque"/> correspondente (o kardex explica cada número). Movimentos manuais não deixam o saldo negativo.
/// </summary>
[DisplayName("Saldo de estoque"), NaoAuditar]
public sealed class SaldoDeEstoque : ITenantOwned
{
    private const int CasasDoCusto = 4;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LojaId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public int Quantidade { get; private set; }

    /// <summary>Custo médio ponderado: (saldo × custo atual + entrada × custo da entrada) ÷ (saldo + entrada).</summary>
    public decimal CustoMedio { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    /// <summary>Concorrência otimista: duas operações simultâneas no mesmo saldo não se sobrepõem.</summary>
    public Guid Versao { get; private set; }

    public decimal ValorEmCusto => Math.Round(Quantidade * CustoMedio, 2);

    private SaldoDeEstoque() { }

    public static SaldoDeEstoque Novo(Guid lojaId, Guid skuId, Guid produtoId) =>
        new() { Id = Guid.NewGuid(), LojaId = lojaId, SkuId = skuId, ProdutoId = produtoId, Versao = Guid.NewGuid() };

    public MovimentoDeEstoque Entrar(TipoDeMovimento tipo, int quantidade, decimal custoUnitario, Operacao op)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        ArgumentOutOfRangeException.ThrowIfNegative(custoUnitario);
        CustoMedio = Quantidade <= 0
            ? custoUnitario
            : Math.Round((Quantidade * CustoMedio + quantidade * custoUnitario) / (Quantidade + quantidade), CasasDoCusto);
        Quantidade += quantidade;
        return Registrar(tipo, quantidade, custoUnitario, op);
    }

    public MovimentoDeEstoque Sair(TipoDeMovimento tipo, int quantidade, Operacao op)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        if (quantidade > Quantidade) throw new EstoqueInsuficienteException(Math.Max(Quantidade, 0), quantidade);
        Quantidade -= quantidade;
        return Registrar(tipo, -quantidade, CustoMedio, op);
    }

    /// <summary>Saída por venda: pelo custo médio; pode deixar o saldo negativo se a empresa permitir (venda não trava por erro de contagem).</summary>
    public MovimentoDeEstoque Vender(int quantidade, Operacao op, bool permitirNegativo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        if (!permitirNegativo && quantidade > Quantidade) throw new EstoqueInsuficienteException(Math.Max(Quantidade, 0), quantidade);
        Quantidade -= quantidade;
        return Registrar(TipoDeMovimento.Venda, -quantidade, CustoMedio, op);
    }

    /// <summary>Estoque inicial (implantação): define a quantidade absoluta e, se informado, o custo. Nulo se nada muda.</summary>
    public MovimentoDeEstoque? DefinirInicial(int quantidade, decimal? custo, Operacao op)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantidade);
        ArgumentOutOfRangeException.ThrowIfNegative(custo ?? 0);
        var diferenca = quantidade - Quantidade;
        if (diferenca == 0 && (custo is null || custo == CustoMedio)) return null;
        Quantidade = quantidade;
        if (custo is not null) CustoMedio = custo.Value;
        return Registrar(TipoDeMovimento.EstoqueInicial, diferenca, CustoMedio, op);
    }

    /// <summary>
    /// Ajuste de inventário pela diferença contada. Sobra entra pelo custo médio (ou <paramref name="custoSeVazio"/> se o saldo
    /// estiver zerado); falta baixa até zerar, nunca negativo. Nulo se nada muda.
    /// </summary>
    public MovimentoDeEstoque? Inventariar(int diferenca, decimal custoSeVazio, Operacao op)
    {
        if (diferenca > 0) return Entrar(TipoDeMovimento.Inventario, diferenca, Quantidade > 0 ? CustoMedio : custoSeVazio, op);
        var baixa = Math.Min(-diferenca, Math.Max(Quantidade, 0));
        return baixa == 0 ? null : Sair(TipoDeMovimento.Inventario, baixa, op);
    }

    private MovimentoDeEstoque Registrar(TipoDeMovimento tipo, int quantidade, decimal custoUnitario, Operacao op)
    {
        AtualizadoEm = op.OcorridoEm;
        Versao = Guid.NewGuid();
        return MovimentoDeEstoque.De(this, tipo, quantidade, custoUnitario, op);
    }
}
