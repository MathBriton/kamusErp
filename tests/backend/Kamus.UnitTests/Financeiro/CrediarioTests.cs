using Kamus.Modules.Financeiro.Dominio;
using Kamus.SharedKernel.Financeiro;
using Shouldly;

namespace Kamus.UnitTests.Financeiro;

public class CrediarioTests
{
    private static readonly ConfiguracaoDoCrediario Padrao = ConfiguracaoDoCrediario.Padrao();

    [Fact]
    public void Padrao_Multa2Juros1AoMesBloqueioCom15Dias()
    {
        (Padrao.MultaPercentual, Padrao.JurosMensalPercentual, Padrao.DiasParaBloquear).ShouldBe((2m, 1m, 15));
    }

    [Fact]
    public void Encargos_EmDiaNaoCobra_AtrasadoCobraMultaUmaVezEJurosProRataDia()
    {
        var vencimento = new DateOnly(2026, 10, 10);
        Encargos.Calcular(100m, vencimento, vencimento, Padrao).ShouldBe(new Encargos(0, 0m, 0m));
        // 15 dias de atraso: multa 2% = 2,00; juros 1% a.m. / 30 × 15 = 0,50
        Encargos.Calcular(100m, vencimento, vencimento.AddDays(15), Padrao).ShouldBe(new Encargos(15, 2m, 0.50m));
        Encargos.Calcular(33.33m, vencimento, vencimento.AddDays(1), Padrao).ShouldBe(new Encargos(1, 0.67m, 0.01m));
    }

    [Fact]
    public void Configuracao_Valida_CDCLimitaMultaA2()
    {
        var c = ConfiguracaoDoCrediario.Padrao();
        Should.Throw<ArgumentException>(() => c.Definir(2.5m, 1m, 15)).Message.ShouldContain("2%");
        Should.Throw<ArgumentException>(() => c.Definir(2m, -1m, 15));
        Should.Throw<ArgumentException>(() => c.Definir(2m, 1m, 0));
        c.Definir(1m, 0.5m, 30);
        (c.MultaPercentual, c.JurosMensalPercentual, c.DiasParaBloquear).ShouldBe((1m, 0.5m, 30));
    }

    [Fact]
    public void Avaliar_LimiteMenosEmAberto_BloqueiaPorAtrasoOuManual()
    {
        var limite = LimiteDeCredito.Definir(Guid.NewGuid(), "Maria", 500m, bloqueado: false, null, "Ana");
        Credito.Avaliar(limite, 120m, 0, Padrao).ShouldBe(new SituacaoDoCredito(500m, 120m, 380m, 0, false, null));
        var atrasada = Credito.Avaliar(limite, 120m, 16, Padrao);
        (atrasada.Bloqueado, atrasada.Motivo).ShouldBe((true, "Parcela atrasada há 16 dias (bloqueia com 15)."));
        Credito.Avaliar(limite, 120m, 15, Padrao).Bloqueado.ShouldBeFalse();
        Credito.Avaliar(null, 0m, 0, Padrao).ShouldBe(new SituacaoDoCredito(0m, 0m, 0m, 0, true, "Cliente sem limite de crediário aprovado."));

        limite.Atualizar("Maria", 500m, bloqueado: true, "Cheque devolvido", "Ana");
        Credito.Avaliar(limite, 0m, 0, Padrao).Motivo.ShouldBe("Crediário bloqueado: Cheque devolvido");
        Credito.Avaliar(LimiteDeCredito.Definir(Guid.NewGuid(), "João", 100m, false, null, "Ana"), 150m, 0, Padrao).Disponivel.ShouldBe(0m);
        Should.Throw<ArgumentException>(() => LimiteDeCredito.Definir(Guid.NewGuid(), "X", -1m, false, null, "Ana"));
    }

    [Fact]
    public void Aging_FaixasDeAtraso()
    {
        Credito.Faixa(0).ShouldBe(FaixaDeAtraso.AVencer);
        Credito.Faixa(1).ShouldBe(FaixaDeAtraso.Ate30);
        Credito.Faixa(30).ShouldBe(FaixaDeAtraso.Ate30);
        Credito.Faixa(31).ShouldBe(FaixaDeAtraso.Ate60);
        Credito.Faixa(90).ShouldBe(FaixaDeAtraso.Ate90);
        Credito.Faixa(91).ShouldBe(FaixaDeAtraso.Acima90);
    }
}
