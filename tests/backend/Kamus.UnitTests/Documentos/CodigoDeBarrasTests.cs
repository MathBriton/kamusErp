using Kamus.SharedKernel.Documentos;

namespace Kamus.UnitTests.Documentos;

public class CodigoDeBarrasTests
{
    [Theory]
    [InlineData("7891000315507")]   // EAN-13
    [InlineData("4006381333931")]
    [InlineData("12345670")]        // EAN-8
    [InlineData(" 789 1234 567895 ")]
    public void Valido_ConfereODigitoVerificador(string codigo) => CodigoDeBarras.Valido(codigo).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7891000315508")]
    [InlineData("789100031550")]
    [InlineData("78910003155A7")]
    public void Valido_RecusaInvalidos(string? codigo) => CodigoDeBarras.Valido(codigo).ShouldBeFalse();

    [Fact]
    public void Normalizar_TiraEspacos() => CodigoDeBarras.Normalizar(" 789 1234 567895 ").ShouldBe("7891234567895");

    [Fact]
    public void Interno_Ean13ComPrefixo2()
    {
        CodigoDeBarras.Interno(1).ShouldBe("2000000000015");
        CodigoDeBarras.Valido(CodigoDeBarras.Interno(98_765)).ShouldBeTrue();
        CodigoDeBarras.SequencialInterno("2000000000015").ShouldBe(1);
        CodigoDeBarras.SequencialInterno("7891000315507").ShouldBeNull();
    }
}
