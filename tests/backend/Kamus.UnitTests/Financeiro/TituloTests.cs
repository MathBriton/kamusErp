using Kamus.Modules.Financeiro.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Financeiro;

public class TituloTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 15);
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Conta = Guid.NewGuid();

    private static DadosDoTitulo Dados(TipoDeTitulo tipo = TipoDeTitulo.Pagar) =>
        new(tipo, "Aluguel da loja", null, "Imobiliária Centro", null, "Contrato 12", new DateOnly(2026, 10, 1), null);

    private static Titulo Criar(params (int Dia, decimal Valor)[] parcelas) =>
        Titulo.Criar(Empresa, Dados(), parcelas.Select(p => new NovaParcela(new DateOnly(2026, 10, p.Dia), p.Valor)).ToList(), Agora);

    [Fact]
    public void Criar_ParcelasNumeradasEmOrdemDeVencimento()
    {
        var titulo = Criar((20, 100m), (10, 50m));

        titulo.ShouldSatisfyAllConditions(
            () => titulo.Tipo.ShouldBe(TipoDeTitulo.Pagar),
            () => titulo.Descricao.ShouldBe("Aluguel da loja"),
            () => titulo.ValorTotal.ShouldBe(150m),
            () => titulo.Origem.ShouldBe("Manual"),
            () => titulo.Parcelas.Select(p => (p.Numero, p.Vencimento.Day, p.Valor)).ShouldBe([(1, 10, 50m), (2, 20, 100m)]));
    }

    [Fact]
    public void Criar_SemDescricaoSemParcelaOuValorNaoPositivo_Erro()
    {
        Should.Throw<ArgumentException>(() => Titulo.Criar(Empresa, Dados() with { Descricao = " " }, [new(Hoje, 10m)], Agora));
        Should.Throw<ArgumentException>(() => Titulo.Criar(Empresa, Dados(), [], Agora));
        Should.Throw<ArgumentException>(() => Titulo.Criar(Empresa, Dados(), [new(Hoje, 0m)], Agora));
        Should.Throw<ArgumentException>(() => Titulo.Criar(Empresa, Dados(), [new(Hoje, 10.001m)], Agora)).Message.ShouldContain("centavos");
    }

    [Fact]
    public void GerarParcelas_DivideEmMesesEOsCentavosVaoNaPrimeira()
    {
        var parcelas = Titulo.GerarParcelas(100m, 3, new DateOnly(2026, 1, 31));

        parcelas.Select(p => p.Valor).ShouldBe([33.34m, 33.33m, 33.33m]);
        parcelas.Select(p => p.Vencimento).ShouldBe([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)]);
    }

    [Fact]
    public void Baixar_ParcialDepoisTotal_ComJurosMultaEDesconto()
    {
        var titulo = Criar((10, 100m));
        var parcela = titulo.Parcelas.Single();

        var primeira = parcela.Baixar(Hoje, Conta, 40m, juros: 2m, multa: 1m, desconto: 0m, "Ana", Agora);
        parcela.Situacao(Hoje).ShouldBe(SituacaoDaParcela.Vencida); // parcial e vencida: o mais urgente
        parcela.Saldo.ShouldBe(60m);
        var segunda = parcela.Baixar(Hoje, Conta, 60m, juros: 0m, multa: 0m, desconto: 5m, "Ana", Agora);

        primeira.Total.ShouldBe(43m);
        segunda.Total.ShouldBe(55m);
        parcela.ValorPago.ShouldBe(100m);
        parcela.Saldo.ShouldBe(0m);
        parcela.Situacao(Hoje).ShouldBe(SituacaoDaParcela.Paga);
        titulo.Quitado.ShouldBeTrue();
    }

    [Fact]
    public void Baixar_AlemDoSaldoOuInvalido_Erro()
    {
        var parcela = Criar((20, 100m)).Parcelas.Single();

        Should.Throw<ArgumentException>(() => parcela.Baixar(Hoje, Conta, 100.01m, 0, 0, 0, null, Agora)).Message.ShouldContain("100,00");
        Should.Throw<ArgumentException>(() => parcela.Baixar(Hoje, Conta, 0m, 0, 0, 0, null, Agora));
        Should.Throw<ArgumentException>(() => parcela.Baixar(Hoje, Conta, 10m, -1, 0, 0, null, Agora));
        Should.Throw<ArgumentException>(() => parcela.Baixar(Hoje, Conta, 10m, 0, 0, 11m, null, Agora)); // desconto maior que o total
    }

    [Fact]
    public void Situacao_AbertaParcialEVencida()
    {
        var parcela = Criar((20, 100m)).Parcelas.Single();

        parcela.Situacao(Hoje).ShouldBe(SituacaoDaParcela.Aberta);
        parcela.Situacao(new DateOnly(2026, 10, 21)).ShouldBe(SituacaoDaParcela.Vencida);
        parcela.Baixar(Hoje, Conta, 10m, 0, 0, 0, null, Agora);
        parcela.Situacao(Hoje).ShouldBe(SituacaoDaParcela.Parcial);
    }

    [Fact]
    public void Estornar_DevolveOSaldo_NaoEstornaDuasVezes()
    {
        var parcela = Criar((20, 100m)).Parcelas.Single();
        var baixa = parcela.Baixar(Hoje, Conta, 100m, 0, 0, 0, null, Agora);

        parcela.Estornar(baixa.Id, "Bia", Agora);

        baixa.Estornada.ShouldBeTrue();
        baixa.EstornadaPor.ShouldBe("Bia");
        parcela.Saldo.ShouldBe(100m);
        parcela.Situacao(Hoje).ShouldBe(SituacaoDaParcela.Aberta);
        Should.Throw<InvalidOperationException>(() => parcela.Estornar(baixa.Id, "Bia", Agora));
    }

    [Fact]
    public void Cancelar_SoSemBaixas_EBloqueiaNovasBaixas()
    {
        var titulo = Criar((20, 100m));
        var comBaixa = Criar((20, 100m));
        comBaixa.Parcelas.Single().Baixar(Hoje, Conta, 10m, 0, 0, 0, null, Agora);

        titulo.Cancelar("Ana", Agora);

        titulo.Cancelado.ShouldBeTrue();
        titulo.Parcelas.Single().Situacao(Hoje).ShouldBe(SituacaoDaParcela.Cancelada);
        Should.Throw<InvalidOperationException>(() => titulo.Parcelas.Single().Baixar(Hoje, Conta, 10m, 0, 0, 0, null, Agora));
        Should.Throw<InvalidOperationException>(() => comBaixa.Cancelar("Ana", Agora)).Message.ShouldContain("estorne");
    }

    [Fact]
    public void Atualizar_MudaOsDadosMasNaoOTipo()
    {
        var titulo = Criar((20, 100m));

        titulo.Atualizar(Dados(TipoDeTitulo.Receber) with { Descricao = "Aluguel (reajustado)", Documento = "C-13" });

        (titulo.Tipo, titulo.Descricao, titulo.Documento).ShouldBe((TipoDeTitulo.Pagar, "Aluguel (reajustado)", "C-13"));
    }
}
