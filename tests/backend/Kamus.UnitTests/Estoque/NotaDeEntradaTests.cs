using System.Text;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.NotasFiscais;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

public class NotaDeEntradaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Produto = Guid.NewGuid();
    private static readonly Guid SkuP = Guid.NewGuid();
    private static readonly Guid SkuM = Guid.NewGuid();

    private static NotaDeEntrada Importar(params ExemploDeNfe.Item[] itens)
    {
        var xml = ExemploDeNfe.Xml(itens);
        return NotaDeEntrada.Importar(Empresa, LeitorDeNfe.Ler(new MemoryStream(Encoding.UTF8.GetBytes(xml))), xml, Agora, "Ana");
    }

    private static readonly ExemploDeNfe.Item Camisetas = new("AUR-100", "SEM GTIN", "CAMISETA BASICA", "UN", 10, 25m, Frete: 10m);
    private static readonly ExemploDeNfe.Item CaixaDeMeias = new("AUR-900", "SEM GTIN", "MEIA CX 12", "CX", 2, 120m);

    [Fact]
    public void Importar_CopiaCabecalhoItensEDuplicatas()
    {
        var nota = Importar(Camisetas, CaixaDeMeias);

        nota.ShouldSatisfyAllConditions(
            () => nota.Status.ShouldBe(StatusDaNota.Pendente),
            () => nota.Chave.ShouldBe(ExemploDeNfe.Chave),
            () => nota.FornecedorDocumento.ShouldBe("11222333000181"),
            () => nota.FornecedorNome.ShouldBe("Aurora Moda"),
            () => nota.ImportadaPor.ShouldBe("Ana"),
            () => nota.EmitidaEm.Offset.ShouldBe(TimeSpan.Zero),
            () => nota.Itens.Select(i => (i.Numero, i.Codigo, i.Situacao)).ShouldBe([(1, "AUR-100", SituacaoDoItem.Pendente), (2, "AUR-900", SituacaoDoItem.Pendente)]),
            () => nota.Duplicatas.Count.ShouldBe(2),
            () => nota.ProntaParaLancar.ShouldBeFalse());
    }

    [Fact]
    public void Distribuir_NaGrade_SomaPrecisaBaterComAQuantidade()
    {
        var item = Importar(Camisetas).Itens.Single();

        Should.Throw<ArgumentException>(() => item.Distribuir(Produto, 1, [(SkuP, 4), (SkuM, 5)], null)).Message.ShouldContain("10");
        item.Distribuir(Produto, 1, [(SkuP, 4), (SkuM, 6)], null);

        item.Situacao.ShouldBe(SituacaoDoItem.Pronto);
        item.Distribuicao.Select(d => (d.SkuId, d.Quantidade)).ShouldBe([(SkuP, 4), (SkuM, 6)]);
        item.CustoPorPeca.ShouldBe(26m); // (250 + 10 de frete) ÷ 10
    }

    [Fact]
    public void Distribuir_ComFator_CaixaVira12Pecas()
    {
        var item = Importar(CaixaDeMeias).Itens.Single();

        item.Distribuir(Produto, 12, [(SkuP, 24)], "de-para");

        item.PecasEsperadas.ShouldBe(24);
        item.CustoPorPeca.ShouldBe(10m); // 240 ÷ 24
        item.OrigemDoVinculo.ShouldBe("de-para");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Distribuir_FatorInvalido_Erro(int fator) =>
        Should.Throw<ArgumentException>(() => Importar(Camisetas).Itens.Single().Distribuir(Produto, fator, [(SkuP, 10)], null));

    [Fact]
    public void Distribuir_SkuRepetidoOuQuantidadeZero_Erro()
    {
        var item = Importar(Camisetas).Itens.Single();

        Should.Throw<ArgumentException>(() => item.Distribuir(Produto, 1, [(SkuP, 5), (SkuP, 5)], null));
        Should.Throw<ArgumentException>(() => item.Distribuir(Produto, 1, [(SkuP, 10), (SkuM, 0)], null));
    }

    [Fact]
    public void Ignorar_ItemQueNaoEhMercadoria()
    {
        var nota = Importar(Camisetas, CaixaDeMeias);
        nota.Itens.First().Distribuir(Produto, 1, [(SkuP, 10)], null);

        nota.Itens.Last().Ignorar(true);

        nota.Itens.Last().Situacao.ShouldBe(SituacaoDoItem.Ignorado);
        nota.ProntaParaLancar.ShouldBeTrue();
    }

    [Fact]
    public void TudoIgnorado_NaoLanca()
    {
        var nota = Importar(Camisetas);
        nota.Itens.Single().Ignorar(true);

        nota.ProntaParaLancar.ShouldBeFalse();
    }

    [Fact]
    public void LancarEDescartar_SoPendente()
    {
        var nota = Importar(Camisetas);
        nota.Itens.Single().Distribuir(Produto, 1, [(SkuP, 10)], null);
        var (loja, operacao, fornecedor) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        nota.MarcarLancada(loja, operacao, fornecedor, Agora, "Bia");

        (nota.Status, nota.LojaId, nota.OperacaoId, nota.FornecedorId, nota.LancadaPor).ShouldBe((StatusDaNota.Lancada, (Guid?)loja, (Guid?)operacao, (Guid?)fornecedor, "Bia"));
        Should.Throw<InvalidOperationException>(() => nota.Descartar());
        Should.Throw<InvalidOperationException>(() => nota.Itens.Single().Ignorar(true));
    }

    [Fact]
    public void QuantidadeFracionada_SoComFatorQueFecheInteiro()
    {
        var item = Importar(new ExemploDeNfe.Item("TEC-1", "SEM GTIN", "TECIDO", "M", 2.5m, 10m)).Itens.Single();

        Should.Throw<ArgumentException>(() => item.Distribuir(Produto, 1, [(SkuP, 2)], null)).Message.ShouldContain("fator");
        item.Distribuir(Produto, 2, [(SkuP, 5)], null);
        item.Situacao.ShouldBe(SituacaoDoItem.Pronto);
    }
}
