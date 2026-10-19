using Kamus.Modules.Vendas.Relatorios;

namespace Kamus.UnitTests.Vendas;

public class CurvaAbcTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(79.9, "A")]
    [InlineData(80, "B")]
    [InlineData(94.9, "B")]
    [InlineData(95, "C")]
    public void Classe_PeloAcumuladoAntesDoProduto(decimal acumuladoAntes, string classe) => RelatorioCurvaAbc.Classe(acumuladoAntes).ShouldBe(classe);
}
