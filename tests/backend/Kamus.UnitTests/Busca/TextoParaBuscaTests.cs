using Kamus.SharedKernel.Busca;

namespace Kamus.UnitTests.Busca;

public class TextoParaBuscaTests
{
    [Theory]
    [InlineData("Ação", "acao")]
    [InlineData("  Ávila  ÇEDILHA ", "avila cedilha")]
    [InlineData("Calça Jeans Skinny", "calca jeans skinny")]
    [InlineData("", "")]
    public void Normalizar_RemoveAcentosEspacosEMaiusculas(string entrada, string esperado)
    {
        TextoParaBusca.Normalizar(entrada).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("Kamus Moda Shopping", "shop", true)]
    [InlineData("Calça Jeans", "calca", true)]
    [InlineData("Kamus Moda Centro", "moda centro", true)]
    [InlineData("Kamus Moda Centro", "shopping", false)]
    public void Contem_IgnoraAcentosEMaiusculas(string texto, string termo, bool esperado)
    {
        TextoParaBusca.Contem(texto, termo).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("12.345.678/0001-95", "12345678", true)]
    [InlineData("12.345.678/0001-95", "12.345", true)]
    [InlineData("12.345.678/0001-95", "999", false)]
    public void ContemDigitos_ComparaSemMascara(string documento, string termo, bool esperado)
    {
        TextoParaBusca.ContemDigitos(documento, termo).ShouldBe(esperado);
    }
}
