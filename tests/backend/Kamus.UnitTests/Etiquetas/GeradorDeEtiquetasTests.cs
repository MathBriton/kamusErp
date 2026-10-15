using System.Text;
using Kamus.Infrastructure.Etiquetas;
using Shouldly;

namespace Kamus.UnitTests.Etiquetas;

public class GeradorDeEtiquetasTests
{
    private static Etiqueta Camiseta(string codigo = "2000000000015") => new("Camiseta Básica Gola V", "CAM-001 · Azul · M", 59.90m, codigo);

    private static int Paginas(byte[] pdf) =>
        System.Text.RegularExpressions.Regex.Count(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]");

    [Fact]
    public void Termica_UmaPaginaPorEtiqueta()
    {
        var pdf = GeradorDeEtiquetas.Gerar([Camiseta(), Camiseta(), Camiseta("7891234567895")], FormatoDeEtiqueta.Termica);

        Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
        Paginas(pdf).ShouldBe(3);
    }

    [Fact]
    public void A4_TrintaPorFolha()
    {
        var pdf = GeradorDeEtiquetas.Gerar(Enumerable.Repeat(Camiseta(), 31).ToList(), FormatoDeEtiqueta.A4);

        Paginas(pdf).ShouldBe(2);
    }

    [Fact]
    public void Gerar_SemEtiquetas_Erro() => Should.Throw<ArgumentOutOfRangeException>(() => GeradorDeEtiquetas.Gerar([], FormatoDeEtiqueta.A4));

    [Theory]
    [InlineData(null, FormatoDeEtiqueta.A4, true)]
    [InlineData("termica", FormatoDeEtiqueta.Termica, true)]
    [InlineData("A4", FormatoDeEtiqueta.A4, true)]
    [InlineData("zebra", FormatoDeEtiqueta.A4, false)]
    public void TentarFormato(string? texto, FormatoDeEtiqueta esperado, bool ok)
    {
        GeradorDeEtiquetas.TentarFormato(texto, out var formato).ShouldBe(ok);
        if (ok) formato.ShouldBe(esperado);
    }
}
