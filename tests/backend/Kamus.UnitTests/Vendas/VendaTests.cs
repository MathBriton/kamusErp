using Kamus.Modules.Vendas.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Vendas;

public class VendaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 17);
    private static readonly Guid SkuA = Guid.NewGuid();
    private static readonly Guid SkuB = Guid.NewGuid();

    private static ItemNovo Item(Guid sku, int quantidade, decimal preco, decimal desconto = 0) => new(sku, Guid.NewGuid(), "CAM-001 Azul M", quantidade, preco, desconto);

    private static PagamentoNovo Pag(TipoDePagamento tipo, decimal valor, int parcelas = 1, decimal taxa = 0) => new(Guid.NewGuid(), tipo.ToString(), tipo, valor, parcelas, taxa);

    private static Venda Criar(IReadOnlyList<ItemNovo> itens, IReadOnlyList<PagamentoNovo> pagamentos, decimal descontoGeral = 0, decimal comissao = 5m) =>
        Venda.Criar(new DadosDaVenda(Guid.NewGuid(), Guid.NewGuid(), 7, Hoje, null, "Maria", Guid.NewGuid(), "Bia", comissao, "NFC-e 123", null),
            itens, descontoGeral, pagamentos, "Ana", Agora);

    [Fact]
    public void Criar_TotaisComDescontosComissaoETaxas()
    {
        var venda = Criar([Item(SkuA, 2, 59.90m, desconto: 9.80m), Item(SkuB, 1, 100m)], [Pag(TipoDePagamento.Credito, 200m, 2, taxa: 5m)], descontoGeral: 10m);

        venda.ShouldSatisfyAllConditions(
            () => venda.Numero.ShouldBe(7),
            () => venda.Subtotal.ShouldBe(210m),
            () => venda.Total.ShouldBe(200m),
            () => venda.Comissao.ShouldBe(10m),
            () => venda.TaxaTotal.ShouldBe(10m),
            () => venda.Troco.ShouldBe(0m),
            () => venda.Status.ShouldBe(StatusDaVenda.Concluida),
            () => venda.Pecas.ShouldBe(3));
        venda.Itens.First().Total.ShouldBe(110m);
    }

    [Fact]
    public void Criar_DinheiroAMaisViraTroco()
    {
        var venda = Criar([Item(SkuA, 1, 59.90m)], [Pag(TipoDePagamento.Pix, 9.90m), Pag(TipoDePagamento.Dinheiro, 100m)]);

        venda.Troco.ShouldBe(50m);
        venda.Pagamentos.Single(p => p.Tipo == TipoDePagamento.Dinheiro).Valor.ShouldBe(50m); // o que fica na loja
    }

    [Fact]
    public void Criar_PagamentosQueNaoFecham_Erro()
    {
        Should.Throw<ArgumentException>(() => Criar([Item(SkuA, 1, 100m)], [Pag(TipoDePagamento.Pix, 90m)])).Message.ShouldContain("faltam R$ 10,00");
        Should.Throw<ArgumentException>(() => Criar([Item(SkuA, 1, 100m)], [Pag(TipoDePagamento.Pix, 110m)])).Message.ShouldContain("troco");
    }

    [Fact]
    public void Criar_ItensOuDescontosInvalidos_Erro()
    {
        Should.Throw<ArgumentException>(() => Criar([], [Pag(TipoDePagamento.Pix, 1m)]));
        Should.Throw<ArgumentException>(() => Criar([Item(SkuA, 0, 10m)], [Pag(TipoDePagamento.Pix, 1m)]));
        Should.Throw<ArgumentException>(() => Criar([Item(SkuA, 1, 10m, desconto: 11m)], [Pag(TipoDePagamento.Pix, 1m)]));
        Should.Throw<ArgumentException>(() => Criar([Item(SkuA, 1, 10m)], [Pag(TipoDePagamento.Pix, 1m)], descontoGeral: 11m));
    }

    [Fact]
    public void CustoELucroBruto_DepoisDaBaixaDeEstoque()
    {
        var venda = Criar([Item(SkuA, 2, 50m)], [Pag(TipoDePagamento.Debito, 100m, taxa: 2m)]);

        venda.RegistrarBaixaDeEstoque(Guid.NewGuid(), new Dictionary<Guid, decimal> { [SkuA] = 20m });

        venda.CustoTotal.ShouldBe(40m);
        venda.LucroBruto.ShouldBe(58m); // 100 − 40 de custo − 2 de taxa
    }

    [Fact]
    public void Cancelar_ExigeMotivoESoUmaVez()
    {
        var venda = Criar([Item(SkuA, 1, 50m)], [Pag(TipoDePagamento.Pix, 50m)]);

        Should.Throw<ArgumentException>(() => venda.Cancelar(" ", "Ana", Agora));
        venda.Cancelar("Cliente desistiu", "Ana", Agora);

        (venda.Status, venda.MotivoDoCancelamento, venda.CanceladaPor).ShouldBe((StatusDaVenda.Cancelada, "Cliente desistiu", "Ana"));
        Should.Throw<InvalidOperationException>(() => venda.Cancelar("de novo", "Ana", Agora));
    }
}
