using Kamus.SharedKernel.Documentos;

namespace Kamus.UnitTests.Documentos;

public class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("11222333000181", "11222333000181")]
    [InlineData(" 11.222.333/0001-81 ", "11222333000181")]
    public void TryParse_CnpjNumericoValido_RetornaValorNormalizado(string entrada, string esperado)
    {
        Cnpj.TryParse(entrada, out var cnpj).ShouldBeTrue();
        cnpj!.Valor.ShouldBe(esperado);
    }

    // Exemplo oficial da Receita Federal para o CNPJ alfanumérico (IN RFB 2.229/2024)
    [Theory]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12abc34501de35", "12ABC34501DE35")]
    public void TryParse_CnpjAlfanumericoValido_RetornaValorNormalizadoEmMaiusculas(string entrada, string esperado)
    {
        Cnpj.TryParse(entrada, out var cnpj).ShouldBeTrue();
        cnpj!.Valor.ShouldBe(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("11222333000182")]     // dígito verificador errado
    [InlineData("12ABC34501DE36")]     // dígito verificador errado (alfanumérico)
    [InlineData("1122233300018")]      // 13 caracteres
    [InlineData("112223330001811")]    // 15 caracteres
    [InlineData("12ABC34501DEAB")]     // dígitos verificadores devem ser numéricos
    [InlineData("11#222333000181")]    // caractere inválido
    [InlineData("00000000000000")]     // sequência repetida
    [InlineData("AAAAAAAAAAAA00")]
    public void TryParse_CnpjInvalido_RetornaFalso(string? entrada)
    {
        Cnpj.TryParse(entrada, out var cnpj).ShouldBeFalse();
        cnpj.ShouldBeNull();
    }

    [Theory]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    [InlineData("12ABC34501DE35", "12.ABC.345/01DE-35")]
    public void Formatado_RetornaComMascara(string entrada, string esperado)
    {
        Cnpj.Parse(entrada).Formatado.ShouldBe(esperado);
    }

    [Fact]
    public void Parse_CnpjInvalido_LancaFormatException()
    {
        Should.Throw<FormatException>(() => Cnpj.Parse("11222333000182"));
    }

    [Fact]
    public void Igualdade_MesmoCnpjComMascarasDiferentes_SaoIguais()
    {
        Cnpj.Parse("11.222.333/0001-81").ShouldBe(Cnpj.Parse("11222333000181"));
    }
}
