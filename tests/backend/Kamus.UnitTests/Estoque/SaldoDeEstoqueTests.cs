using Kamus.Modules.Estoque.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

public class SaldoDeEstoqueTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Loja = Guid.NewGuid();
    private static readonly Guid Sku = Guid.NewGuid();
    private static readonly Guid Produto = Guid.NewGuid();

    private static Operacao Op(string? motivo = null) => new(Guid.NewGuid(), Agora, Guid.NewGuid(), "Ana", motivo, "NF 123", null, null);

    private static SaldoDeEstoque Novo() => SaldoDeEstoque.Novo(Loja, Sku, Produto);

    [Fact]
    public void Entrar_PrimeiraEntrada_CustoMedioEhOCustoDaEntrada()
    {
        var saldo = Novo();

        var movimento = saldo.Entrar(TipoDeMovimento.Entrada, 10, 20m, Op());

        saldo.Quantidade.ShouldBe(10);
        saldo.CustoMedio.ShouldBe(20m);
        movimento.ShouldSatisfyAllConditions(
            () => movimento.Tipo.ShouldBe(TipoDeMovimento.Entrada),
            () => movimento.Quantidade.ShouldBe(10),
            () => movimento.CustoUnitario.ShouldBe(20m),
            () => movimento.SaldoApos.ShouldBe(10),
            () => movimento.CustoMedioApos.ShouldBe(20m),
            () => movimento.Documento.ShouldBe("NF 123"),
            () => movimento.UsuarioNome.ShouldBe("Ana"),
            () => movimento.LojaId.ShouldBe(Loja),
            () => movimento.SkuId.ShouldBe(Sku),
            () => movimento.ProdutoId.ShouldBe(Produto));
    }

    [Fact]
    public void Entrar_ComSaldo_CustoMedioPonderado()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 10, 20m, Op());

        saldo.Entrar(TipoDeMovimento.Entrada, 5, 26m, Op());

        saldo.Quantidade.ShouldBe(15);
        saldo.CustoMedio.ShouldBe(22m); // (10 × 20 + 5 × 26) / 15
    }

    [Fact]
    public void Entrar_CustoMedioComDizimaArredondaEmQuatroCasas()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 2, 10m, Op());

        saldo.Entrar(TipoDeMovimento.Entrada, 1, 11m, Op());

        saldo.CustoMedio.ShouldBe(10.3333m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Entrar_QuantidadeNaoPositiva_Erro(int quantidade) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Novo().Entrar(TipoDeMovimento.Entrada, quantidade, 1m, Op()));

    [Fact]
    public void Entrar_CustoNegativo_Erro() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Novo().Entrar(TipoDeMovimento.Entrada, 1, -1m, Op()));

    [Fact]
    public void Sair_BaixaSemMudarCustoMedioEGuardaQuantidadeNegativaNoMovimento()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 10, 20m, Op());

        var movimento = saldo.Sair(TipoDeMovimento.Ajuste, 3, Op("Avaria"));

        saldo.Quantidade.ShouldBe(7);
        saldo.CustoMedio.ShouldBe(20m);
        movimento.Quantidade.ShouldBe(-3);
        movimento.CustoUnitario.ShouldBe(20m);
        movimento.SaldoApos.ShouldBe(7);
        movimento.Motivo.ShouldBe("Avaria");
    }

    [Fact]
    public void Sair_MaisQueOSaldo_EstoqueInsuficiente()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 2, 20m, Op());

        var erro = Should.Throw<EstoqueInsuficienteException>(() => saldo.Sair(TipoDeMovimento.Ajuste, 3, Op()));

        erro.Disponivel.ShouldBe(2);
        saldo.Quantidade.ShouldBe(2);
    }

    [Fact]
    public void Sair_TudoEDepoisEntrar_CustoMedioRecomecaPelaNovaEntrada()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 2, 20m, Op());
        saldo.Sair(TipoDeMovimento.Ajuste, 2, Op());

        saldo.Entrar(TipoDeMovimento.Entrada, 1, 30m, Op());

        saldo.CustoMedio.ShouldBe(30m);
    }

    [Fact]
    public void DefinirInicial_ValorAbsolutoGeraMovimentoPelaDiferenca()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 4, 20m, Op());

        var movimento = saldo.DefinirInicial(10, 25m, Op())!;

        saldo.Quantidade.ShouldBe(10);
        saldo.CustoMedio.ShouldBe(25m);
        movimento.Tipo.ShouldBe(TipoDeMovimento.EstoqueInicial);
        movimento.Quantidade.ShouldBe(6);
        movimento.SaldoApos.ShouldBe(10);
    }

    [Fact]
    public void DefinirInicial_SemCusto_MantemCustoMedio_EIgualNaoGeraMovimento()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 4, 20m, Op());

        saldo.DefinirInicial(1, null, Op())!.Quantidade.ShouldBe(-3);
        saldo.CustoMedio.ShouldBe(20m);
        saldo.DefinirInicial(1, 20m, Op()).ShouldBeNull();
    }

    [Fact]
    public void DefinirInicial_Negativo_Erro() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Novo().DefinirInicial(-1, null, Op()));

    [Fact]
    public void ValorEmCusto_QuantidadeVezesCustoMedio()
    {
        var saldo = Novo();
        saldo.Entrar(TipoDeMovimento.Entrada, 3, 10.50m, Op());

        saldo.ValorEmCusto.ShouldBe(31.50m);
    }
}
