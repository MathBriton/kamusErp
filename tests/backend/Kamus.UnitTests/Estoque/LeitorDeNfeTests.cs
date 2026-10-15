using System.Text;
using Kamus.Modules.Estoque.NotasFiscais;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

public class LeitorDeNfeTests
{
    private static NfeLida Ler(string xml) => LeitorDeNfe.Ler(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private static readonly ExemploDeNfe.Item Camiseta = new("AUR-100-AZ-M", "SEM GTIN", "CAMISETA BASICA AZUL M", "UN", 10, 25m, Frete: 10m, Desconto: 5m, Ipi: 2.5m, IcmsSt: 7.5m);
    private static readonly ExemploDeNfe.Item Calca = new("AUR-200", "7891234567895", "CALCA JEANS", "CX", 2, 300m);

    [Fact]
    public void Ler_CabecalhoEmitenteDestinatarioEDuplicatas()
    {
        var nota = Ler(ExemploDeNfe.Xml([Camiseta, Calca]));

        nota.ShouldSatisfyAllConditions(
            () => nota.Chave.ShouldBe(ExemploDeNfe.Chave),
            () => nota.Numero.ShouldBe("1234"),
            () => nota.Serie.ShouldBe("1"),
            () => nota.EmitidaEm.ShouldBe(new DateTimeOffset(2026, 10, 10, 10, 30, 0, TimeSpan.FromHours(-3))),
            () => nota.Emitente.Documento.ShouldBe("11222333000181"),
            () => nota.Emitente.RazaoSocial.ShouldBe("Confecções Aurora LTDA"),
            () => nota.Emitente.NomeFantasia.ShouldBe("Aurora Moda"),
            () => nota.Emitente.Telefone.ShouldBe("1133334444"),
            () => nota.Emitente.Cidade.ShouldBe("São Paulo"),
            () => nota.Emitente.Uf.ShouldBe("SP"),
            () => nota.DestinatarioDocumento.ShouldBe("12345678000195"),
            () => nota.ValorTotal.ShouldBe(865m),
            () => nota.Autorizada.ShouldBeTrue(),
            () => nota.Duplicatas.Select(d => (d.Numero, d.Vencimento, d.Valor)).ShouldBe([("001", new DateOnly(2026, 11, 10), 432.5m), ("002", new DateOnly(2026, 12, 10), 432.5m)]));
    }

    [Fact]
    public void Ler_ItensComCustoDeAquisicao()
    {
        var itens = Ler(ExemploDeNfe.Xml([Camiseta, Calca])).Itens;

        itens.Count.ShouldBe(2);
        var camiseta = itens[0];
        camiseta.ShouldSatisfyAllConditions(
            () => camiseta.Numero.ShouldBe(1),
            () => camiseta.Codigo.ShouldBe("AUR-100-AZ-M"),
            () => camiseta.Ean.ShouldBeNull(), // "SEM GTIN"
            () => camiseta.Descricao.ShouldBe("CAMISETA BASICA AZUL M"),
            () => camiseta.Ncm.ShouldBe("61091000"),
            () => camiseta.Unidade.ShouldBe("UN"),
            () => camiseta.Quantidade.ShouldBe(10m),
            () => camiseta.ValorTotal.ShouldBe(250m),
            () => camiseta.CustoTotal.ShouldBe(265m)); // 250 + frete 10 − desconto 5 + IPI 2,50 + ST 7,50
        itens[1].Ean.ShouldBe("7891234567895");
        itens[1].CustoTotal.ShouldBe(600m);
    }

    [Fact]
    public void Ler_SoNFeSemProtocolo_NaoAutorizada()
    {
        var nota = Ler(ExemploDeNfe.Xml([Camiseta], cStat: null));

        nota.Autorizada.ShouldBeFalse();
        nota.Itens.Count.ShouldBe(1);
    }

    [Fact]
    public void Ler_ProtocoloDeRejeicao_Erro() =>
        Should.Throw<NfeInvalidaException>(() => Ler(ExemploDeNfe.Xml([Camiseta], cStat: "301"))).Message.ShouldContain("não está autorizada");

    [Theory]
    [InlineData("isto não é xml")]
    [InlineData("<html><body>oi</body></html>")]
    [InlineData("<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"><infNFe Id=\"NFe123\"/></NFe>")]
    public void Ler_ArquivoQueNaoEhNfe_Erro(string xml) => Should.Throw<NfeInvalidaException>(() => Ler(xml));

    [Fact]
    public void Ler_DtdExterno_Recusado()
    {
        const string xxe = """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><NFe xmlns="http://www.portalfiscal.inf.br/nfe">&e;</NFe>""";

        Should.Throw<NfeInvalidaException>(() => Ler(xxe));
    }
}
