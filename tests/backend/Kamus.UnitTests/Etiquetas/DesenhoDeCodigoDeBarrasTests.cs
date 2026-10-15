using Kamus.Infrastructure.Etiquetas;
using Shouldly;

namespace Kamus.UnitTests.Etiquetas;

public class DesenhoDeCodigoDeBarrasTests
{
    private static string Bits(IReadOnlyList<bool> modulos) => string.Concat(modulos.Select(m => m ? '1' : '0'));

    [Fact]
    public void Ean13_95ModulosComGuardasEParidadePeloPrimeiroDigito()
    {
        // Exemplo clássico 5 901234 123457: o primeiro dígito (5) define a paridade LGGLLG da metade esquerda.
        var bits = Bits(DesenhoDeCodigoDeBarras.Ean13("5901234123457"));

        bits.Length.ShouldBe(95);
        bits[..3].ShouldBe("101");
        bits.Substring(45, 5).ShouldBe("01010");
        bits[^3..].ShouldBe("101");
        bits.Substring(3, 7).ShouldBe("0001011");  // 9 em L
        bits.Substring(10, 7).ShouldBe("0100111"); // 0 em G
        bits.Substring(50, 7).ShouldBe("1100110"); // 1 em R
    }

    [Fact]
    public void Ean8_67Modulos()
    {
        var bits = Bits(DesenhoDeCodigoDeBarras.Ean8("96385074"));

        bits.Length.ShouldBe(67);
        bits[..3].ShouldBe("101");
        bits.Substring(3, 7).ShouldBe("0001011"); // 9 em L
        bits.Substring(31, 5).ShouldBe("01010");
    }

    [Fact]
    public void Svg_UpcAViraEan13ComZeroAEsquerda()
    {
        var upc = DesenhoDeCodigoDeBarras.Svg("036000291452");
        var ean = DesenhoDeCodigoDeBarras.Svg("0036000291452");

        upc.ShouldBe(ean);
        upc.ShouldStartWith("<svg");
        upc.ShouldContain("<rect");
    }

    [Fact]
    public void Svg_Gtin14UsaItf14()
    {
        var svg = DesenhoDeCodigoDeBarras.Svg("17891234567892");

        svg.ShouldStartWith("<svg");
        DesenhoDeCodigoDeBarras.Itf("17891234567892").Count.ShouldBeGreaterThan(100);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("5901234123458")] // dígito verificador errado
    [InlineData("abc")]
    public void Svg_CodigoInvalido_Erro(string codigo) => Should.Throw<ArgumentException>(() => DesenhoDeCodigoDeBarras.Svg(codigo));
}
