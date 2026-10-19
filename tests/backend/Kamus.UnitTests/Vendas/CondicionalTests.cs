using Kamus.Modules.Vendas.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Vendas;

public class CondicionalTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 19);
    private static readonly Guid SkuA = Guid.NewGuid();
    private static readonly Guid SkuB = Guid.NewGuid();

    private static Condicional Abrir(DateOnly? prazo = null) => Condicional.Abrir(
        new DadosDaCondicional(Guid.NewGuid(), Guid.NewGuid(), 4, Hoje, prazo ?? Hoje.AddDays(2), Guid.NewGuid(), "Maria", Guid.NewGuid(), "Bia", null, null),
        [new ItemNovo(SkuA, Guid.NewGuid(), "Camiseta Azul M", 2, 59.90m, 0), new ItemNovo(SkuB, Guid.NewGuid(), "Vestido P", 1, 89.90m, 0),
            new ItemNovo(SkuA, Guid.NewGuid(), "Camiseta Azul M", 1, 59.90m, 0)],
        "Ana", Agora);

    [Fact]
    public void Abrir_JuntaOMesmoSku_ValorEPecas_SituacaoPeloPrazo()
    {
        var c = Abrir();
        (c.Numero, c.Pecas, c.Valor, c.Itens.Count).ShouldBe((4, 4, 269.60m, 2));
        c.Situacao(Hoje.AddDays(2)).ShouldBe(SituacaoDaCondicional.Aberta);
        c.Situacao(Hoje.AddDays(3)).ShouldBe(SituacaoDaCondicional.Atrasada);
        Should.Throw<ArgumentException>(() => Abrir(prazo: Hoje.AddDays(-1))).Message.ShouldContain("prazo");
    }

    [Fact]
    public void Conferir_DevolvidasNaoPassamDoLevado_RestoViraVenda()
    {
        var c = Abrir();
        Should.Throw<ArgumentException>(() => c.Conferir(new Dictionary<Guid, int> { [SkuA] = 4 })).Message.ShouldContain("levou 3");
        Should.Throw<ArgumentException>(() => c.Conferir(new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 })).Message.ShouldContain("não está");

        var ficou = c.Conferir(new Dictionary<Guid, int> { [SkuA] = 2 });
        ficou.Select(i => (i.SkuId, i.Quantidade)).ShouldBe([(SkuA, 1), (SkuB, 1)]);

        var venda = Guid.NewGuid();
        c.Fechar(new Dictionary<Guid, int> { [SkuA] = 2 }, venda, 8, "Ana", Agora);
        (c.Situacao(Hoje), c.VendaId, c.VendaNumero, c.PecasVendidas, c.ValorVendido).ShouldBe((SituacaoDaCondicional.Fechada, venda, 8, 2, 149.80m));
        Should.Throw<InvalidOperationException>(() => c.Fechar(new Dictionary<Guid, int>(), null, null, "Ana", Agora)).Message.ShouldContain("fechada");
    }

    [Fact]
    public void Fechar_TudoDevolvido_SemVenda()
    {
        var c = Abrir();
        c.Fechar(new Dictionary<Guid, int> { [SkuA] = 3, [SkuB] = 1 }, null, null, "Ana", Agora);
        (c.PecasVendidas, c.ValorVendido, c.VendaId).ShouldBe((0, 0m, (Guid?)null));
        Should.Throw<ArgumentException>(() => Abrir().Fechar(new Dictionary<Guid, int>(), null, null, "Ana", Agora)).Message.ShouldContain("venda");
    }

    [Fact]
    public void Limites_PorPecasEValorSomandoAsAbertas()
    {
        var config = ConfiguracaoDeVendas.Padrao(Guid.NewGuid());
        (config.PrazoCondicionalDias, config.LimiteCondicionalPecas, config.LimiteCondicionalValor).ShouldBe((2, 10, 1000m));
        Condicional.ConferirLimites(config, pecasAbertas: 6, valorAberto: 300m, pecasNovas: 4, valorNovo: 200m).ShouldBeNull();
        Condicional.ConferirLimites(config, 6, 300m, 5, 200m).ShouldBe("O cliente ficaria com 11 peças em condicional (limite de 10).");
        Condicional.ConferirLimites(config, 0, 900m, 1, 150m).ShouldBe("O cliente ficaria com R$ 1.050,00 em condicional (limite de R$ 1.000,00).");
        config.Definir(true, prazoCondicionalDias: 5, limiteCondicionalPecas: 0, limiteCondicionalValor: 0m);
        Condicional.ConferirLimites(config, 50, 9000m, 10, 500m).ShouldBeNull(); // 0 = sem limite
    }
}
