using Kamus.SharedKernel.Documentos;

namespace Kamus.UnitTests.Documentos;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData(" 111.444.777-35 ", "11144477735")]
    public void TryParse_CpfValido_NormalizaSemMascara(string entrada, string esperado)
    {
        Cpf.TryParse(entrada, out var cpf).ShouldBeTrue();
        cpf!.Valor.ShouldBe(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("529.982.247-24")]
    [InlineData("111.111.111-11")]
    [InlineData("5299822472")]
    [InlineData("529982247255")]
    [InlineData("529.982.247-2A")]
    public void TryParse_CpfInvalido_Recusa(string? entrada) => Cpf.TryParse(entrada, out _).ShouldBeFalse();

    [Fact]
    public void Formatado_ComMascara() => Cpf.Parse("52998224725").Formatado.ShouldBe("529.982.247-25");
}
