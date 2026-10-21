using Kamus.SharedKernel.Consultas;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.UnitTests.Consultas;

public class PaginacaoTests
{
    private static readonly int[] Numeros = Enumerable.Range(1, 45).ToArray();
    private static readonly int[] SegundaPagina = Enumerable.Range(21, 20).ToArray();
    private static readonly int[] UltimaPagina = [41, 42, 43, 44, 45];

    [Fact]
    public void Paginar_PaginaDoMeio_TrazOTrechoEOTotal()
    {
        var p = Numeros.Paginar(pagina: 2, tamanho: 20);

        p.Itens.ShouldBe(SegundaPagina);
        (p.Total, p.Pagina, p.Tamanho).ShouldBe((45, 2, 20));
    }

    [Fact]
    public void Paginar_UltimaPaginaIncompleta()
    {
        Numeros.Paginar(3, 20).Itens.ShouldBe(UltimaPagina);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 500, 1, 200)]
    public void Paginar_ValoresForaDoLimite_SaoAjustados(int pagina, int tamanho, int paginaEsperada, int tamanhoEsperado)
    {
        var p = Numeros.Paginar(pagina, tamanho);

        (p.Pagina, p.Tamanho).ShouldBe((paginaEsperada, tamanhoEsperado));
    }

    [Fact]
    public void Ids_LeGuidsSeparadosPorVirgulaEIgnoraLixo()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var parametros = new Dictionary<string, string?> { ["ids"] = $"{a}, {b},x,,{a}" };

        ParametrosDoRelatorio.Ids(parametros).ShouldBe([a, b], ignoreOrder: true);
        ParametrosDoRelatorio.Ids(new Dictionary<string, string?>()).ShouldBeEmpty();
    }

    [Fact]
    public void Selecionados_FiltroAplicadoNoSingularENoPlural()
    {
        ParametrosDoRelatorio.Selecionados(1, "produto", "produtos").ShouldBe(new FiltroAplicado("Selecionados", "1 produto"));
        ParametrosDoRelatorio.Selecionados(3, "produto", "produtos").ShouldBe(new FiltroAplicado("Selecionados", "3 produtos"));
    }
}
