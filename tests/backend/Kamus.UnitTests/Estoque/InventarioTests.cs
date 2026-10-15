using Kamus.Modules.Estoque.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

public class InventarioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Loja = Guid.NewGuid();
    private static readonly Guid Produto = Guid.NewGuid();
    private static readonly Guid SkuA = Guid.NewGuid();
    private static readonly Guid SkuB = Guid.NewGuid();
    private static readonly Guid SkuC = Guid.NewGuid();

    private static Inventario Abrir() =>
        Inventario.Abrir(Loja, [new FotoDoSaldo(SkuA, Produto, 5), new FotoDoSaldo(SkuB, Produto, 3)], Agora, "Ana", "Balanço de outubro");

    [Fact]
    public void Abrir_GuardaAFotoDoSaldo()
    {
        var inventario = Abrir();

        inventario.Status.ShouldBe(StatusDoInventario.Aberto);
        inventario.Itens.Select(i => (i.SkuId, i.SaldoNaAbertura, i.Contado)).ShouldBe([(SkuA, 5, (int?)null), (SkuB, 3, null)], ignoreOrder: true);
        inventario.AbertoPor.ShouldBe("Ana");
        inventario.Observacao.ShouldBe("Balanço de outubro");
    }

    [Fact]
    public void Contar_DefineOuSoma_SkuForaDaFotoComecaDoZero()
    {
        var inventario = Abrir();

        inventario.Contar(SkuA, Produto, 4, somar: false, Agora);
        inventario.Contar(SkuC, Produto, 1, somar: true, Agora);
        inventario.Contar(SkuC, Produto, 1, somar: true, Agora);

        inventario.Itens.Single(i => i.SkuId == SkuA).Contado.ShouldBe(4);
        var novo = inventario.Itens.Single(i => i.SkuId == SkuC);
        (novo.SaldoNaAbertura, novo.Contado).ShouldBe((0, 2));
    }

    [Fact]
    public void Contar_Negativo_Erro()
    {
        var inventario = Abrir();
        inventario.Contar(SkuA, Produto, 1, somar: false, Agora);

        Should.Throw<ArgumentOutOfRangeException>(() => inventario.Contar(SkuA, Produto, -1, somar: false, Agora));
        Should.Throw<ArgumentOutOfRangeException>(() => inventario.Contar(SkuA, Produto, -2, somar: true, Agora));
    }

    [Fact]
    public void LimparContagem_VoltaANaoContado()
    {
        var inventario = Abrir();
        inventario.Contar(SkuA, Produto, 4, somar: false, Agora);

        inventario.LimparContagem(SkuA);

        inventario.Itens.Single(i => i.SkuId == SkuA).Contado.ShouldBeNull();
    }

    [Fact]
    public void Diferencas_SoContadosOuZerandoNaoContados()
    {
        var inventario = Abrir();
        inventario.Contar(SkuA, Produto, 4, somar: false, Agora);
        inventario.Contar(SkuC, Produto, 2, somar: false, Agora);

        inventario.Diferencas(zerarNaoContados: false).ShouldBe([(SkuA, -1), (SkuC, 2)], ignoreOrder: true);
        inventario.Diferencas(zerarNaoContados: true).ShouldBe([(SkuA, -1), (SkuB, -3), (SkuC, 2)], ignoreOrder: true);
    }

    [Fact]
    public void ConcluirECancelar_SoAberto()
    {
        var inventario = Abrir();
        var operacao = Guid.NewGuid();

        inventario.Concluir(Agora, "Bia", operacao, zerouNaoContados: true);

        inventario.Status.ShouldBe(StatusDoInventario.Concluido);
        (inventario.ConcluidoPor, inventario.OperacaoId, inventario.ZerouNaoContados).ShouldBe(("Bia", (Guid?)operacao, true));
        Should.Throw<InvalidOperationException>(() => inventario.Contar(SkuA, Produto, 1, somar: false, Agora));
        Should.Throw<InvalidOperationException>(() => inventario.Cancelar(Agora, "Bia"));
    }

    [Fact]
    public void Inventariar_SomaOuBaixaSemFicarNegativo()
    {
        var op = new Operacao(Guid.NewGuid(), Agora, null, "Ana", "Inventário", null, null, null);
        var saldo = SaldoDeEstoque.Novo(Loja, SkuA, Produto);
        saldo.Entrar(TipoDeMovimento.Entrada, 2, 10m, op);

        saldo.Inventariar(3, 99m, op)!.ShouldSatisfyAllConditions(m => m.Tipo.ShouldBe(TipoDeMovimento.Inventario), m => m.Quantidade.ShouldBe(3));
        saldo.CustoMedio.ShouldBe(10m); // sobra entra pelo custo médio vigente
        saldo.Inventariar(-9, 99m, op)!.Quantidade.ShouldBe(-5); // não passa de zero
        saldo.Quantidade.ShouldBe(0);
        saldo.Inventariar(0, 99m, op).ShouldBeNull();
        saldo.Inventariar(1, 7m, op);
        saldo.CustoMedio.ShouldBe(7m); // saldo zerado: usa o custo informado
    }
}
