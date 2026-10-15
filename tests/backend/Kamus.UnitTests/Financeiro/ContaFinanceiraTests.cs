using Kamus.Modules.Financeiro.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Financeiro;

public class ContaFinanceiraTests
{
    [Fact]
    public void Criar_ComSaldoInicial()
    {
        var conta = ContaFinanceira.Criar(Guid.NewGuid(), " Caixa da loja ", TipoDeConta.Caixa, 150m, new DateOnly(2026, 10, 1));

        (conta.Nome, conta.Tipo, conta.SaldoInicial, conta.Ativa).ShouldBe(("Caixa da loja", TipoDeConta.Caixa, 150m, true));
    }

    [Fact]
    public void Criar_SemNome_Erro() =>
        Should.Throw<ArgumentException>(() => ContaFinanceira.Criar(Guid.NewGuid(), "", TipoDeConta.Banco, 0m, new DateOnly(2026, 10, 1)));

    [Fact]
    public void Categorias_PadroesTemReceitasEDespesas()
    {
        Categoria.Padroes.ShouldContain(c => c.Nome == "Compra de mercadorias" && c.Tipo == TipoDeCategoria.Despesa);
        Categoria.Padroes.ShouldContain(c => c.Nome == "Vendas" && c.Tipo == TipoDeCategoria.Receita);
        Categoria.Padroes.Select(c => c.Nome).ShouldBeUnique();
    }

    [Fact]
    public void Movimento_DaBaixa_ReceberEntraPagarSai()
    {
        var conta = Guid.NewGuid();
        var agora = DateTimeOffset.UtcNow;
        var pagar = Titulo.Criar(Guid.NewGuid(), new DadosDoTitulo(TipoDeTitulo.Pagar, "Luz", null, null, null, null, new DateOnly(2026, 10, 1), null),
            [new NovaParcela(new DateOnly(2026, 10, 20), 80m)], agora);
        var receber = Titulo.Criar(Guid.NewGuid(), new DadosDoTitulo(TipoDeTitulo.Receber, "Venda", null, null, null, null, new DateOnly(2026, 10, 1), null),
            [new NovaParcela(new DateOnly(2026, 10, 20), 80m)], agora);
        var hoje = new DateOnly(2026, 10, 15);

        var saida = MovimentoFinanceiro.DaBaixa(pagar, pagar.Parcelas.Single(), pagar.Parcelas.Single().Baixar(hoje, conta, 80m, 2m, 0, 0, "Ana", agora), agora);
        var entrada = MovimentoFinanceiro.DaBaixa(receber, receber.Parcelas.Single(), receber.Parcelas.Single().Baixar(hoje, conta, 80m, 0, 0, 0, "Ana", agora), agora);

        saida.Valor.ShouldBe(-82m);
        saida.Descricao.ShouldBe("Luz (1/1)");
        entrada.Valor.ShouldBe(80m);
        entrada.ContaId.ShouldBe(conta);
    }
}
