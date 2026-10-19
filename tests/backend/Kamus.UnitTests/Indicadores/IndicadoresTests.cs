using Kamus.Modules.Indicadores.Aplicacao;
using Kamus.Modules.Indicadores.Dominio;
using Kamus.SharedKernel.Indicadores;

namespace Kamus.UnitTests.Indicadores;

public class PeriodoTests
{
    [Fact]
    public void Escolher_SeteDias_ComparaComOsSeteDiasAnteriores()
    {
        var p = Periodo.Escolher("7dias", new DateOnly(2026, 10, 19))!;

        (p.De, p.Ate, p.AnteriorDe, p.AnteriorAte).ShouldBe((new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void Mes_DiaQueNaoExisteNoMesAnterior_ComparaAteOFimDele()
    {
        var p = Periodo.Mes(new DateOnly(2026, 3, 31));

        (p.De, p.AnteriorDe, p.AnteriorAte).ShouldBe((new DateOnly(2026, 3, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void Escolher_CodigoDesconhecido_Nulo() => Periodo.Escolher("ano", new DateOnly(2026, 10, 19)).ShouldBeNull();

    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(50, 100, -50)]
    [InlineData(10, 3, 233.3)]
    public void Variacao_EmPercentual(decimal atual, decimal anterior, decimal esperado) => Painel.Variacao(atual, anterior).ShouldBe(esperado);

    [Fact]
    public void Variacao_SemAnterior_Nula() => Painel.Variacao(100, 0).ShouldBeNull();
}

public class AlertasTests
{
    private static readonly Compromissos Nada = new(0, 0, 0, 0, 0);

    [Fact]
    public void Alertas_SoParaOQueTemProblema_ComLinkParaODetalhe()
    {
        var financeiro = new ResumoFinanceiro([], 0, Nada with { Vencido = 80, ParcelasVencidas = 2 }, Nada, 80, 1);
        var estoque = new ResumoDeEstoque(10, 100, 200, 0, [new QuebraDeGrade(Guid.NewGuid(), "Camiseta", ["Azul P", "Azul M"])], 1);

        var alertas = Painel.Alertas(financeiro, estoque, new CondicionaisNaRua(1, 0, 2, 50), verCrediario: false);

        alertas.Select(a => (a.Tipo, a.Titulo, a.Link)).ShouldBe([
            ("contas_a_receber_vencidas", "2 contas a receber vencidas", "/financeiro/contas-a-receber"),
            ("quebra_de_grade", "1 produto com quebra de grade", "/estoque/posicao")]);
        alertas[1].Detalhe.ShouldBe("Camiseta: Azul P, Azul M esgotados");
    }
}

public class PreferenciasDoResumoTests
{
    private static readonly DateTime SegundaAs20 = new(2026, 10, 19, 20, 5, 0);

    [Fact]
    public void Padrao_LigadoSoParaODono()
    {
        PreferenciasDoResumo.Padrao(Guid.NewGuid(), dono: true).Ativo.ShouldBeTrue();
        PreferenciasDoResumo.Padrao(Guid.NewGuid(), dono: false).Ativo.ShouldBeFalse();
    }

    [Fact]
    public void DeveEnviar_DepoisDoHorarioNoDiaEscolhido_UmaVezPorDia()
    {
        var p = PreferenciasDoResumo.Padrao(Guid.NewGuid(), dono: true);

        p.DeveEnviar(SegundaAs20.AddHours(-1)).ShouldBeFalse();
        p.DeveEnviar(SegundaAs20).ShouldBeTrue();
        p.MarcarEnvio(DateOnly.FromDateTime(SegundaAs20));
        p.DeveEnviar(SegundaAs20.AddHours(2)).ShouldBeFalse();
        p.DeveEnviar(SegundaAs20.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void DeveEnviar_DiaNaoEscolhido_NaoEnvia()
    {
        var p = PreferenciasDoResumo.Padrao(Guid.NewGuid(), dono: true);
        p.Alterar(true, 8, [2, 3, 4, 5, 6], true, true, true, true, true, false);

        p.DeveEnviar(SegundaAs20).ShouldBeFalse();
        p.DiasDaSemana().ShouldBe([2, 3, 4, 5, 6]);
    }

    [Theory]
    [InlineData(24, new[] { 1 }, "hora")]
    [InlineData(8, new[] { 7 }, "dias")]
    [InlineData(8, new int[0], "ao menos um dia")]
    public void Alterar_ValoresInvalidos_Recusa(int hora, int[] dias, string trecho)
    {
        var p = PreferenciasDoResumo.Padrao(Guid.NewGuid(), dono: true);

        Should.Throw<ArgumentException>(() => p.Alterar(true, hora, dias, true, true, true, true, true, false)).Message.ShouldContain(trecho, Case.Insensitive);
    }
}
