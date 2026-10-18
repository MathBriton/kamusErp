using Kamus.Modules.Vendas.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Vendas;

public class TrocaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 20);
    private static readonly Guid SkuA = Guid.NewGuid();
    private static readonly Guid SkuB = Guid.NewGuid();
    private static readonly Guid SkuNovo = Guid.NewGuid();

    private static PagamentoNovo Pag(TipoDePagamento tipo, decimal valor) => new(Guid.NewGuid(), tipo.ToString(), tipo, valor, 1, 0);

    /// <summary>2 × A a 59,90 + 1 × B a 100 = 219,80; desconto geral de 19,80 → total 200 (pago 200 em PIX), vendedora Bia com 5%.</summary>
    private static Venda Venda()
    {
        var venda = Kamus.Modules.Vendas.Dominio.Venda.Criar(
            new DadosDaVenda(Guid.NewGuid(), Guid.NewGuid(), 7, Hoje.AddDays(-3), Guid.NewGuid(), "Maria", Guid.NewGuid(), "Bia", 5m, null, null),
            [new ItemNovo(SkuA, Guid.NewGuid(), "Camiseta Azul M", 2, 59.90m, 0), new ItemNovo(SkuB, Guid.NewGuid(), "Calça 40", 1, 100m, 0)],
            19.80m, [Pag(TipoDePagamento.Pix, 200m)], "Ana", Agora.AddDays(-3));
        venda.RegistrarBaixaDeEstoque(Guid.NewGuid(), new Dictionary<Guid, decimal> { [SkuA] = 20m, [SkuB] = 45m });
        return venda;
    }

    private static Troca Trocar(Venda venda, IReadOnlyList<DevolucaoNova> devolvidos, IReadOnlyList<ItemNovo>? levados = null,
        IReadOnlyList<PagamentoNovo>? pagamentos = null, IReadOnlyDictionary<Guid, int>? jaDevolvidos = null) =>
        Troca.Criar(new DadosDaTroca(venda.EmpresaId, venda.LojaId, 3, Hoje, MotivoDaTroca.Tamanho, null, null), venda, devolvidos, levados ?? [],
            pagamentos ?? [], jaDevolvidos ?? new Dictionary<Guid, int>(), "Ana", Agora);

    [Fact]
    public void Criar_ValorDevolvidoEhOPagoComODescontoDaVendaRateado()
    {
        var troca = Trocar(Venda(), [new DevolucaoNova(SkuA, 1, DestinoDaDevolucao.Estoque)]);

        // linha A: 119,80 × (200 / 219,80) = 109,01 → metade = 54,51
        troca.ShouldSatisfyAllConditions(
            () => troca.ValorDevolvido.ShouldBe(54.51m),
            () => troca.ValorLevado.ShouldBe(0m),
            () => troca.Diferenca.ShouldBe(-54.51m),
            () => troca.ValorDoVale.ShouldBe(54.51m),
            () => troca.Comissao.ShouldBe(-2.73m),
            () => troca.VendaNumero.ShouldBe(7),
            () => troca.ClienteNome.ShouldBe("Maria"),
            () => troca.VendedorNome.ShouldBe("Bia"));
        troca.Devolvidos.Single().CustoUnitario.ShouldBe(20m);
    }

    [Fact]
    public void Criar_PecaMaisCaraClientePagaADiferenca()
    {
        var troca = Trocar(Venda(), [new DevolucaoNova(SkuA, 1, DestinoDaDevolucao.Estoque)],
            [new ItemNovo(SkuNovo, Guid.NewGuid(), "Vestido P", 1, 89.90m, 0)], [Pag(TipoDePagamento.Dinheiro, 40m)]);

        (troca.ValorLevado, troca.Diferenca, troca.ValorDoVale, troca.Troco).ShouldBe((89.90m, 35.39m, 0m, 4.61m));
        troca.Pagamentos.Single().Valor.ShouldBe(35.39m);
        troca.Comissao.ShouldBe(1.77m);
    }

    [Fact]
    public void Criar_Validacoes()
    {
        var venda = Venda();
        Should.Throw<ArgumentException>(() => Trocar(venda, [])).Message.ShouldContain("Escolha");
        Should.Throw<ArgumentException>(() => Trocar(venda, [new DevolucaoNova(SkuNovo, 1, DestinoDaDevolucao.Estoque)])).Message.ShouldContain("não está na venda");
        Should.Throw<ArgumentException>(() => Trocar(venda, [new DevolucaoNova(SkuA, 3, DestinoDaDevolucao.Estoque)])).Message.ShouldContain("2");
        Should.Throw<ArgumentException>(() => Trocar(venda, [new DevolucaoNova(SkuA, 2, DestinoDaDevolucao.Estoque)], jaDevolvidos: new Dictionary<Guid, int> { [SkuA] = 1 }))
            .Message.ShouldContain("resta 1");
        Should.Throw<ArgumentException>(() => Trocar(venda, [new DevolucaoNova(SkuA, 1, DestinoDaDevolucao.Estoque)],
            [new ItemNovo(SkuNovo, Guid.NewGuid(), "Vestido", 1, 89.90m, 0)])).Message.ShouldContain("faltam R$ 35,39");
        Should.Throw<ArgumentException>(() => Trocar(venda, [new DevolucaoNova(SkuA, 1, DestinoDaDevolucao.Estoque)], [], [Pag(TipoDePagamento.Pix, 10m)]))
            .Message.ShouldContain("vale-troca");

        venda.Cancelar("Teste", "Ana", Agora);
        Should.Throw<InvalidOperationException>(() => Trocar(venda, [new DevolucaoNova(SkuA, 1, DestinoDaDevolucao.Estoque)])).Message.ShouldContain("cancelada");
    }

    [Fact]
    public void Criar_DevolveTudoComDefeito_ValeComOTotalPago()
    {
        var troca = Trocar(Venda(), [new DevolucaoNova(SkuA, 2, DestinoDaDevolucao.Avariado), new DevolucaoNova(SkuB, 1, DestinoDaDevolucao.Estoque)]);
        troca.ValorDevolvido.ShouldBe(200m); // o rateio fecha exatamente o total pago
        troca.Devolvidos.Count(d => d.Destino == DestinoDaDevolucao.Avariado).ShouldBe(1);
    }
}

public class ValeTrocaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    private static ValeTroca Vale(decimal valor = 54.50m) => ValeTroca.Emitir(Guid.NewGuid(), "VT7K2M9Q", Guid.NewGuid(), "Maria", valor, Hoje, 90, Guid.NewGuid());

    [Fact]
    public void Emitir_ValidadeEUsoParcialComSaldo()
    {
        var vale = Vale();
        (vale.ValidoAte, vale.Saldo, vale.Situacao(Hoje)).ShouldBe((Hoje.AddDays(90), 54.50m, SituacaoDoVale.Ativo));

        vale.Usar(30m, Hoje.AddDays(10));
        vale.Saldo.ShouldBe(24.50m);
        Should.Throw<InvalidOperationException>(() => vale.Usar(30m, Hoje.AddDays(10))).Message.ShouldContain("R$ 24,50");
        vale.Usar(24.50m, Hoje.AddDays(11));
        vale.Situacao(Hoje.AddDays(11)).ShouldBe(SituacaoDoVale.Usado);

        vale.Devolver(30m);
        vale.Saldo.ShouldBe(30m);
    }

    [Fact]
    public void Usar_VencidoOuCanceladoRecusa()
    {
        var vale = Vale();
        vale.Situacao(Hoje.AddDays(91)).ShouldBe(SituacaoDoVale.Vencido);
        Should.Throw<InvalidOperationException>(() => vale.Usar(10m, Hoje.AddDays(91))).Message.ShouldContain("venceu");

        vale.Cancelar("Emitido por engano", "Ana");
        Should.Throw<InvalidOperationException>(() => vale.Usar(10m, Hoje)).Message.ShouldContain("cancelado");
    }

    [Fact]
    public void NovoCodigo_OitoCaracteresSemAmbiguos()
    {
        var codigos = Enumerable.Range(0, 200).Select(_ => ValeTroca.NovoCodigo()).ToList();
        codigos.ShouldAllBe(c => c.Length == 8 && c.StartsWith("VT", StringComparison.Ordinal) && !c.Substring(2).Any(ch => "0O1I".Contains(ch)));
        codigos.Distinct().Count().ShouldBeGreaterThan(195);
    }
}

public class FechamentoDeComissaoTests
{
    [Fact]
    public void Fechar_MarcaVendasETrocas_VendaFechadaNaoCancela()
    {
        var venda = Venda.Criar(new DadosDaVenda(Guid.NewGuid(), Guid.NewGuid(), 1, new DateOnly(2026, 10, 5), null, null, Guid.NewGuid(), "Bia", 5m, null, null),
            [new ItemNovo(Guid.NewGuid(), Guid.NewGuid(), "X", 1, 100m, 0)], 0, [new PagamentoNovo(Guid.NewGuid(), "PIX", TipoDePagamento.Pix, 100m, 1, 0)],
            "Ana", DateTimeOffset.UtcNow);

        var fechamento = FechamentoDeComissao.Fechar(venda.EmpresaId, venda.VendedorId!.Value, "Bia", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            [venda], [], "Ana", DateTimeOffset.UtcNow);

        (fechamento.Vendas, fechamento.TotalVendido, fechamento.Comissao).ShouldBe((1, 100m, 5m));
        venda.FechamentoId.ShouldBe(fechamento.Id);
        Should.Throw<InvalidOperationException>(() => venda.Cancelar("Desistiu", "Ana", DateTimeOffset.UtcNow)).Message.ShouldContain("reabra");
        Should.Throw<ArgumentException>(() => FechamentoDeComissao.Fechar(venda.EmpresaId, venda.VendedorId!.Value, "Bia", new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31), [], [], "Ana", DateTimeOffset.UtcNow)).Message.ShouldContain("Nada");
    }
}
