using Kamus.Infrastructure.Planilhas;
using Shouldly;

namespace Kamus.UnitTests.Planilhas;

public class ValoresDaPlanilhaTests
{
    [Theory]
    [InlineData("59.9", 59.90)]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("10,005", 10.0)]
    public void Numero_FormatosAceitos_ConverteEArredonda(string texto, double esperado) =>
        ValoresDaPlanilha.Numero(texto).ShouldBe(((decimal?)esperado, true));

    [Fact]
    public void Numero_VazioOuInvalido_DistingueOsDois()
    {
        ValoresDaPlanilha.Numero(" ").ShouldBe((null, true));
        ValoresDaPlanilha.Numero("abc").ShouldBe((null, false));
    }

    [Fact]
    public void Data_IsoOuBrasileira_Converte()
    {
        ValoresDaPlanilha.Data("2026-10-14").Valor.ShouldBe(new DateOnly(2026, 10, 14));
        ValoresDaPlanilha.Data("14/10/2026").Valor.ShouldBe(new DateOnly(2026, 10, 14));
        ValoresDaPlanilha.Data("31/02/2026").Ok.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Sim", true)]
    [InlineData("não", false)]
    [InlineData("talvez", null)]
    public void SimNao_ReconheceVariacoes(string texto, bool? esperado) => ValoresDaPlanilha.SimNao(texto).ShouldBe(esperado);
}
