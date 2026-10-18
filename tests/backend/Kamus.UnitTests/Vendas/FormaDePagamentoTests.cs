using Kamus.Modules.Vendas.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Vendas;

public class FormaDePagamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 17);

    [Fact]
    public void Credito_ParceladoTaxaDeParceladoEVencimentosMensaisDescontandoATaxa()
    {
        var credito = FormaDePagamento.Criar(Guid.NewGuid(), "Cartão de crédito", TipoDePagamento.Credito, null, taxaAVista: 3m, taxaParcelado: 5m, maxParcelas: 6, prazoDias: 30);

        var recebimentos = credito.Recebimentos(100m, 3, Hoje);

        credito.Taxa(1).ShouldBe(3m);
        credito.Taxa(3).ShouldBe(5m);
        recebimentos.Select(r => (r.Vencimento, r.Valor)).ShouldBe(
            [(Hoje.AddDays(30), 31.68m), (Hoje.AddDays(60), 31.66m), (Hoje.AddDays(90), 31.66m)]); // líquido R$ 95,00 dividido (centavos na 1ª)
        credito.EntraNaHora.ShouldBeFalse();
    }

    [Fact]
    public void Dinheiro_EntraNaHoraEmParcelaUnica()
    {
        var dinheiro = FormaDePagamento.Criar(Guid.NewGuid(), "Dinheiro", TipoDePagamento.Dinheiro, Guid.NewGuid(), 0, 0, 1, 0);

        dinheiro.EntraNaHora.ShouldBeTrue();
        dinheiro.Recebimentos(80m, 1, Hoje).ShouldBe([new Kamus.SharedKernel.Financeiro.ParcelaAReceber(Hoje, 80m)]);
    }

    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(0, 101, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 25)]
    public void Criar_TaxaOuParcelasInvalidas_Erro(decimal aVista, decimal parcelado, int max) =>
        Should.Throw<ArgumentException>(() => FormaDePagamento.Criar(Guid.NewGuid(), "X", TipoDePagamento.Credito, null, aVista, parcelado, max, 30));

    [Fact]
    public void Padroes_DinheiroPixDebitoCreditoEValeTroca()
    {
        FormaDePagamento.Padroes.Select(p => p.Tipo).ShouldBe([TipoDePagamento.Dinheiro, TipoDePagamento.Pix, TipoDePagamento.Debito, TipoDePagamento.Credito, TipoDePagamento.ValeTroca]);
    }
}
