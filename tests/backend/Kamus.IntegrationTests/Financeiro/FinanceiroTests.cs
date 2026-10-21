using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Financeiro;

/// <summary>Contas financeiras, categorias, contas a pagar/receber (parcelas, baixas, estorno), transferências, extrato e fluxo de caixa.</summary>
public sealed class FinanceiroTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record ContaDto(Guid Id, string Nome, string Tipo, decimal SaldoInicial, decimal Saldo, bool Ativa);
    private sealed record CategoriaDto(Guid Id, string Nome, string Grupo, string Tipo, bool Ativa);
    private sealed record BaixaDto(Guid Id, DateOnly Data, string Conta, decimal Valor, decimal Juros, decimal Multa, decimal Desconto, decimal Total, bool Estornada);
    private sealed record ParcelaDto(Guid Id, int Numero, DateOnly Vencimento, decimal Valor, decimal ValorPago, decimal Saldo, string Situacao, List<BaixaDto> Baixas);
    private sealed record TituloDto(Guid Id, string Tipo, string Descricao, string? PessoaNome, string? Categoria, decimal ValorTotal, bool Cancelado, string Origem, List<ParcelaDto> Parcelas);
    private sealed record LinhaDto(Guid TituloId, Guid ParcelaId, int Numero, int Parcelas, DateOnly Vencimento, string Descricao, string? PessoaNome, decimal Valor,
        decimal ValorPago, decimal Saldo, string Situacao);
    private sealed record ListaDto(List<LinhaDto> Itens, decimal Valor, decimal Pago, decimal Saldo, decimal Vencido, int Total = 0, int Pagina = 0, int Tamanho = 0);
    private sealed record PaginaDeCategoriasDto(List<CategoriaDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record PaginaDeContasDto(List<ContaDto> Itens, int Total, int Pagina, int Tamanho, decimal Saldo);
    private sealed record FalhaDto(Guid ParcelaId, string Descricao, string Motivo);
    private sealed record BaixaEmLoteDto(int Baixadas, decimal Total, List<FalhaDto> Falhas);
    private sealed record MovimentoDto(DateOnly Data, string Descricao, decimal Valor, decimal Saldo);
    private sealed record ExtratoDto(decimal SaldoAnterior, List<MovimentoDto> Movimentos, decimal SaldoFinal);
    private sealed record DiaDto(DateOnly Data, decimal EntradasRealizadas, decimal SaidasRealizadas, decimal EntradasPrevistas, decimal SaidasPrevistas, decimal Saldo);
    private sealed record FluxoDto(decimal SaldoInicial, List<DiaDto> Periodos, decimal AReceberEmAtraso, decimal APagarEmAtraso, decimal SaldoFinal);

    private static async Task<ContaDto> CriarConta(HttpClient client, string nome, decimal saldo = 0m, string tipo = "Caixa")
    {
        var r = await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome, tipo, saldoInicial = saldo, saldoInicialEm = Hoje.AddDays(-30) }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<ContaDto>(Ct))!;
    }

    private static async Task<List<CategoriaDto>> CategoriasPadrao(HttpClient client)
    {
        (await client.PostAsync(U("/api/financeiro/categorias/padrao"), null, Ct)).EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<List<CategoriaDto>>(U("/api/financeiro/categorias"), Ct))!;
    }

    private static async Task<TituloDto> CriarTitulo(HttpClient client, object corpo)
    {
        var r = await client.PostAsJsonAsync(U("/api/financeiro/titulos"), corpo, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<TituloDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> Baixar(HttpClient client, Guid parcelaId, Guid contaId, decimal valor, decimal juros = 0, decimal desconto = 0, DateOnly? data = null) =>
        client.PostAsJsonAsync(U($"/api/financeiro/parcelas/{parcelaId}/baixas"), new { data = data ?? Hoje, contaId, valor, juros, multa = 0m, desconto }, Ct);

    private static async Task<List<ContaDto>> Contas(HttpClient client) => (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!;

    [Fact]
    public async Task Contas_CriarComSaldoInicialENomeUnico()
    {
        var client = await Entrar(_api);

        var caixa = await CriarConta(client, "Caixa da loja", 150m);

        caixa.ShouldBe(caixa with { Tipo = "Caixa", SaldoInicial = 150m, Saldo = 150m, Ativa = true });
        (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "caixa da loja", tipo = "Banco", saldoInicial = 0, saldoInicialEm = Hoje }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "X", tipo = "Cofre", saldoInicial = 0, saldoInicialEm = Hoje }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var shopping = await Entrar(_api, empresa: "Kamus Moda Shopping");
        (await Contas(shopping)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Categorias_PadraoUmaVezSo_ECriarPropria()
    {
        var client = await Entrar(_api);

        var categorias = await CategoriasPadrao(client);
        var deNovo = await CategoriasPadrao(client);
        var nova = await client.PostAsJsonAsync(U("/api/financeiro/categorias"), new { nome = "Manutenção da loja", grupo = "Ocupação", tipo = "Despesa" }, Ct);

        categorias.ShouldContain(c => c.Nome == "Aluguel" && c.Grupo == "Ocupação" && c.Tipo == "Despesa");
        deNovo.Count.ShouldBe(categorias.Count);
        nova.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(U("/api/financeiro/categorias"), new { nome = "aluguel", grupo = "X", tipo = "Despesa" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ContaAPagar_ParcelasBaixaParcialComJurosEstornoEExtrato()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa", 500m);
        var aluguel = (await CategoriasPadrao(client)).Single(c => c.Nome == "Aluguel");

        var titulo = await CriarTitulo(client, new
        {
            tipo = "Pagar",
            descricao = "Reforma da vitrine",
            pessoaNome = "Marcenaria Silva",
            categoriaId = aluguel.Id,
            documento = "Orçamento 7",
            competencia = Hoje,
            parcelas = new[] { new { vencimento = Hoje.AddDays(10), valor = 200m }, new { vencimento = Hoje.AddDays(40), valor = 100m } },
        });
        titulo.ShouldSatisfyAllConditions(t => t.ValorTotal.ShouldBe(300m), t => t.Categoria.ShouldBe("Aluguel"), t => t.Parcelas.Count.ShouldBe(2));
        var primeira = titulo.Parcelas[0];

        var parcial = await Baixar(client, primeira.Id, caixa.Id, 80m, juros: 5m);
        parcial.StatusCode.ShouldBe(HttpStatusCode.OK, await parcial.Content.ReadAsStringAsync(Ct));
        var depois = (await parcial.Content.ReadFromJsonAsync<TituloDto>(Ct))!.Parcelas[0];
        depois.ShouldSatisfyAllConditions(p => p.ValorPago.ShouldBe(80m), p => p.Saldo.ShouldBe(120m), p => p.Situacao.ShouldBe("Parcial"));
        depois.Baixas.Single().ShouldBe(depois.Baixas.Single() with { Conta = "Caixa", Valor = 80m, Juros = 5m, Total = 85m, Estornada = false });
        (await Contas(client)).Single().Saldo.ShouldBe(415m);
        (await Baixar(client, primeira.Id, caixa.Id, 120.01m)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var estorno = await client.PostAsync(U($"/api/financeiro/baixas/{depois.Baixas.Single().Id}/estornar"), null, Ct);
        estorno.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Contas(client)).Single().Saldo.ShouldBe(500m);
        (await client.PostAsync(U($"/api/financeiro/baixas/{depois.Baixas.Single().Id}/estornar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var extrato = (await client.GetFromJsonAsync<ExtratoDto>(U($"/api/financeiro/contas/{caixa.Id}/extrato?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct))!;
        extrato.SaldoAnterior.ShouldBe(500m);
        extrato.Movimentos.Select(m => (m.Descricao, m.Valor, m.Saldo)).ShouldBe(
            [("Reforma da vitrine (1/2)", -85m, 415m), ("Estorno: Reforma da vitrine (1/2)", 85m, 500m)]);
        extrato.SaldoFinal.ShouldBe(500m);
        (await client.GetStringAsync(U("/api/auditoria"), Ct)).ShouldContain("baixa_estornada");
    }

    [Fact]
    public async Task Listagem_FiltrosDeSituacaoETotais()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa", 1000m);
        await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(-3), valor = 120m } } });
        var agua = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Água", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(5), valor = 60m } } });
        await CriarTitulo(client, new { tipo = "Receber", descricao = "Venda a prazo", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(5), valor = 300m } } });
        (await Baixar(client, agua.Parcelas[0].Id, caixa.Id, 60m)).EnsureSuccessStatusCode();

        var abertas = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas"), Ct))!;
        var vencidas = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=vencidas"), Ct))!;
        var pagas = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=pagas"), Ct))!;
        var todas = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=todas&busca=agu"), Ct))!;

        abertas.Itens.Select(i => (i.Descricao, i.Situacao)).ShouldBe([("Luz", "Vencida")]);
        abertas.ShouldSatisfyAllConditions(a => a.Saldo.ShouldBe(120m), a => a.Vencido.ShouldBe(120m));
        vencidas.Itens.Single().Descricao.ShouldBe("Luz");
        pagas.Itens.Single().ShouldBe(pagas.Itens.Single() with { Descricao = "Água", Situacao = "Paga", ValorPago = 60m, Saldo = 0m, Numero = 1, Parcelas = 1 });
        todas.Itens.Select(i => i.Descricao).ShouldBe(["Água"]);
        (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=receber&situacao=abertas"), Ct))!.Saldo.ShouldBe(300m);
    }

    [Fact]
    public async Task RepetirMensal_DespesaFixaGeraUmTituloPorMes()
    {
        var client = await Entrar(_api);
        var vencimento = new DateOnly(Hoje.Year, Hoje.Month, 5).AddMonths(1);

        var r = await client.PostAsJsonAsync(U("/api/financeiro/titulos"), new
        {
            tipo = "Pagar",
            descricao = "Aluguel",
            competencia = vencimento,
            repetirMeses = 3,
            parcelas = new[] { new { vencimento, valor = 2500m } },
        }, Ct);

        r.StatusCode.ShouldBe(HttpStatusCode.Created);
        var lista = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas"), Ct))!;
        lista.Itens.Select(i => (i.Descricao, i.Vencimento)).ShouldBe(
            [("Aluguel", vencimento), ("Aluguel", vencimento.AddMonths(1)), ("Aluguel", vencimento.AddMonths(2))]);
    }

    [Fact]
    public async Task Validacoes_CategoriaDoTipoErradoContaDeOutraEmpresaECancelamento()
    {
        var client = await Entrar(_api);
        var vendas = (await CategoriasPadrao(client)).Single(c => c.Nome == "Vendas");
        var contaDoShopping = await CriarConta(await Entrar(_api, empresa: "Kamus Moda Shopping"), "Caixa Shopping");
        var luz = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 100m } } });

        (await client.PostAsJsonAsync(U("/api/financeiro/titulos"), new { tipo = "Pagar", descricao = "X", categoriaId = vendas.Id, competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 1m } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Baixar(client, luz.Parcelas[0].Id, contaDoShopping.Id, 10m)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PostAsync(U($"/api/financeiro/titulos/{luz.Id}/cancelar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelado = (await client.GetFromJsonAsync<TituloDto>(U($"/api/financeiro/titulos/{luz.Id}"), Ct))!;
        (cancelado.Cancelado, cancelado.Parcelas[0].Situacao).ShouldBe((true, "Cancelada"));
        (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas"), Ct))!.Itens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Transferencia_MoveEntreContasDaEmpresa()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa", 300m);
        var banco = await CriarConta(client, "Banco", 0m, "Banco");

        var r = await client.PostAsJsonAsync(U("/api/financeiro/transferencias"), new { origemId = caixa.Id, destinoId = banco.Id, data = Hoje, valor = 250m, observacao = "Depósito" }, Ct);

        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        (await Contas(client)).Select(c => (c.Nome, c.Saldo)).ShouldBe([("Banco", 250m), ("Caixa", 50m)]);
        (await client.PostAsJsonAsync(U("/api/financeiro/transferencias"), new { origemId = caixa.Id, destinoId = caixa.Id, data = Hoje, valor = 1m }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FluxoDeCaixa_RealizadoAteHojePrevistoDepoisEAtrasados()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa", 1000m);
        var pago = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Fornecedor", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 200m } } });
        (await Baixar(client, pago.Parcelas[0].Id, caixa.Id, 200m)).EnsureSuccessStatusCode();
        await CriarTitulo(client, new { tipo = "Receber", descricao = "Cliente", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(2), valor = 500m } } });
        await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(3), valor = 150m } } });
        await CriarTitulo(client, new { tipo = "Pagar", descricao = "Atrasada", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(-5), valor = 70m } } });

        var fluxo = (await client.GetFromJsonAsync<FluxoDto>(U($"/api/financeiro/fluxo?de={Hoje:yyyy-MM-dd}&ate={Hoje.AddDays(4):yyyy-MM-dd}"), Ct))!;

        fluxo.SaldoInicial.ShouldBe(1000m);
        fluxo.Periodos.Count.ShouldBe(5);
        fluxo.Periodos[0].ShouldBe(new DiaDto(Hoje, 0m, 200m, 0m, 0m, 800m));
        fluxo.Periodos[2].ShouldBe(new DiaDto(Hoje.AddDays(2), 0m, 0m, 500m, 0m, 1300m));
        fluxo.Periodos[3].ShouldBe(new DiaDto(Hoje.AddDays(3), 0m, 0m, 0m, 150m, 1150m));
        (fluxo.APagarEmAtraso, fluxo.AReceberEmAtraso, fluxo.SaldoFinal).ShouldBe((70m, 0m, 1150m));
    }

    [Fact]
    public async Task Permissoes_VendedorEEstoquistaNaoAcessam()
    {
        var vendedor = await Entrar(_api, "vendedor@kamus.local");

        (await vendedor.GetAsync(U("/api/financeiro/titulos?tipo=pagar"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.GetAsync(U("/api/financeiro/contas"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Relatorios_ContasEFluxoEmCsv()
    {
        var client = await Entrar(_api);
        await CriarConta(client, "Caixa", 100m);
        await CriarTitulo(client, new { tipo = "Pagar", descricao = "Internet", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(1), valor = 99.9m } } });

        var contas = await client.GetStringAsync(U("/api/relatorios/titulos.csv?tipo=pagar&situacao=abertas"), Ct);
        var fluxo = await client.GetStringAsync(U($"/api/relatorios/fluxo-de-caixa.csv?de={Hoje:yyyy-MM-dd}&ate={Hoje.AddDays(2):yyyy-MM-dd}"), Ct);

        contas.ShouldContain("Internet");
        fluxo.ShouldContain(Hoje.AddDays(1).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Listagem_PaginadaComTotaisSobreTodasAsLinhas_SemPaginaDevolveTudo()
    {
        var client = await Entrar(_api);
        foreach (var (descricao, dias, valor) in new[] { ("Luz", 1, 10m), ("Água", 2, 20m), ("Internet", 3, 30m) })
            await CriarTitulo(client, new { tipo = "Pagar", descricao, competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(dias), valor } } });

        var pagina = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas&pagina=2&tamanho=2"), Ct))!;
        var tudo = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas"), Ct))!;

        pagina.Itens.Select(i => i.Descricao).ShouldBe(["Internet"]);
        (pagina.Total, pagina.Pagina, pagina.Tamanho, pagina.Saldo, pagina.Valor).ShouldBe((3, 2, 2, 60m, 60m));
        tudo.Itens.Count.ShouldBe(3);
        tudo.Total.ShouldBe(3);
    }

    [Fact]
    public async Task BaixaEmLote_QuitaAsSelecionadasHojeNaConta_EInformaAsQueNaoDeram()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa", 1000m);
        var luz = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(-2), valor = 120m } } });
        var agua = await CriarTitulo(client, new
        {
            tipo = "Pagar",
            descricao = "Água",
            competencia = Hoje,
            parcelas = new[] { new { vencimento = Hoje.AddDays(5), valor = 60m }, new { vencimento = Hoje.AddDays(35), valor = 40m } },
        });
        var internet = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Internet", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 99m } } });
        (await Baixar(client, agua.Parcelas[0].Id, caixa.Id, 25m)).EnsureSuccessStatusCode();
        (await client.PostAsync(U($"/api/financeiro/titulos/{internet.Id}/cancelar"), null, Ct)).EnsureSuccessStatusCode();

        var r = await client.PostAsJsonAsync(U("/api/financeiro/parcelas/baixas-em-lote"),
            new { parcelaIds = new[] { luz.Parcelas[0].Id, agua.Parcelas[0].Id, internet.Parcelas[0].Id }, contaId = caixa.Id }, Ct);

        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var resultado = (await r.Content.ReadFromJsonAsync<BaixaEmLoteDto>(Ct))!;
        (resultado.Baixadas, resultado.Total).ShouldBe((2, 155m));
        resultado.Falhas.Single().ShouldBe(new FalhaDto(internet.Parcelas[0].Id, "Internet", "Título cancelado não recebe baixa."));
        (await Contas(client)).Single().Saldo.ShouldBe(1000m - 25m - 155m);
        var aguaDepois = (await client.GetFromJsonAsync<TituloDto>(U($"/api/financeiro/titulos/{agua.Id}"), Ct))!;
        aguaDepois.Parcelas.Select(p => p.Situacao).ShouldBe(["Paga", "Aberta"]);
        aguaDepois.Parcelas[0].Baixas.Last().ShouldBe(aguaDepois.Parcelas[0].Baixas.Last() with { Data = Hoje, Conta = "Caixa", Valor = 35m, Juros = 0m, Total = 35m });
    }

    [Fact]
    public async Task BaixaEmLote_ValidaSelecaoContaEPermissao()
    {
        var client = await Entrar(_api);
        var luz = await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 100m } } });
        var contaDoShopping = await CriarConta(await Entrar(_api, empresa: "Kamus Moda Shopping"), "Caixa Shopping");
        var caixa = await CriarConta(client, "Caixa");
        static object Corpo(Guid conta, params Guid[] ids) => new { parcelaIds = ids, contaId = conta };

        (await client.PostAsJsonAsync(U("/api/financeiro/parcelas/baixas-em-lote"), Corpo(caixa.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/financeiro/parcelas/baixas-em-lote"), Corpo(contaDoShopping.Id, luz.Parcelas[0].Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var vendedor = await Entrar(_api, "vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/financeiro/parcelas/baixas-em-lote"), Corpo(caixa.Id, luz.Parcelas[0].Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetFromJsonAsync<TituloDto>(U($"/api/financeiro/titulos/{luz.Id}"), Ct))!.Parcelas[0].Situacao.ShouldBe("Aberta");
    }

    [Fact]
    public async Task Relatorio_Titulos_SoAsParcelasSelecionadas()
    {
        var client = await Entrar(_api);
        var agua = await CriarTitulo(client, new
        {
            tipo = "Pagar",
            descricao = "Água",
            competencia = Hoje,
            parcelas = new[] { new { vencimento = Hoje.AddDays(5), valor = 61m }, new { vencimento = Hoje.AddDays(35), valor = 42m } },
        });
        await CriarTitulo(client, new { tipo = "Pagar", descricao = "Luz", competencia = Hoje, parcelas = new[] { new { vencimento = Hoje, valor = 100m } } });

        var csv = await client.GetStringAsync(U($"/api/relatorios/titulos.csv?ids={agua.Parcelas[1].Id}"), Ct);

        csv.ShouldContain("Água");
        static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        csv.ShouldContain(Br(Hoje.AddDays(35)));
        csv.ShouldNotContain(Br(Hoje.AddDays(5)));
        csv.ShouldNotContain("Luz");
        var dados = await client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/titulos/dados?ids={agua.Parcelas[1].Id},{Guid.NewGuid()}"), Ct);
        dados.GetProperty("filtros")[0].GetProperty("valor").GetString().ShouldBe("1 parcela");
    }

    [Fact]
    public async Task Categorias_FiltrosPaginacaoSituacaoEmLoteERelatorio()
    {
        var client = await Entrar(_api);
        var todas = await CategoriasPadrao(client);
        var despesas = todas.Where(c => c.Tipo == "Despesa").ToList();
        var (aluguel, salarios) = (despesas.Single(c => c.Nome == "Aluguel"), despesas.Single(c => c.Nome == "Salários"));

        var pagina = (await client.GetFromJsonAsync<PaginaDeCategoriasDto>(U("/api/financeiro/categorias?tipo=Despesa&situacao=ativas&pagina=1&tamanho=3"), Ct))!;
        (pagina.Total, pagina.Itens.Count, pagina.Pagina, pagina.Tamanho).ShouldBe((despesas.Count, 3, 1, 3));
        pagina.Itens.ShouldAllBe(c => c.Tipo == "Despesa");
        (await client.GetFromJsonAsync<PaginaDeCategoriasDto>(U("/api/financeiro/categorias?busca=alug&pagina=1&tamanho=20"), Ct))!.Itens.Single().Id.ShouldBe(aluguel.Id);

        var lote = await client.PostAsJsonAsync(U("/api/financeiro/categorias/situacao"), new { ids = new[] { aluguel.Id, salarios.Id }, ativo = false }, Ct);
        lote.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await lote.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("alterados").GetInt32().ShouldBe(2);
        (await client.GetFromJsonAsync<PaginaDeCategoriasDto>(U("/api/financeiro/categorias?situacao=inativas&pagina=1&tamanho=20"), Ct))!.Total.ShouldBe(2);
        (await client.GetFromJsonAsync<List<CategoriaDto>>(U("/api/financeiro/categorias"), Ct))!.Count.ShouldBe(todas.Count);
        (await client.PostAsJsonAsync(U("/api/financeiro/categorias/situacao"), new { ids = Array.Empty<Guid>(), ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var vendedor = await Entrar(_api, "vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/financeiro/categorias/situacao"), new { ids = new[] { aluguel.Id }, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var csv = await client.GetStringAsync(U($"/api/relatorios/categorias-financeiras.csv?ids={aluguel.Id}"), Ct);
        csv.ShouldContain("Aluguel");
        csv.ShouldNotContain("Salários");
        (await client.GetStringAsync(U("/api/relatorios/categorias-financeiras.csv?tipo=Receita&situacao=todas"), Ct)).ShouldNotContain("Aluguel");
    }

    [Fact]
    public async Task ContasFinanceiras_PaginacaoSaldoTotalSituacaoEmLoteERelatorio()
    {
        var client = await Entrar(_api);
        var caixa = await CriarConta(client, "Caixa");
        var banco = await CriarConta(client, "Banco", 0m, "Banco");
        var cofre = await CriarConta(client, "Cofre", 250m);

        var pagina = (await client.GetFromJsonAsync<PaginaDeContasDto>(U("/api/financeiro/contas?situacao=ativas&pagina=1&tamanho=2"), Ct))!;
        (pagina.Total, pagina.Itens.Count, pagina.Saldo).ShouldBe((3, 2, 250m));
        (await client.GetFromJsonAsync<PaginaDeContasDto>(U("/api/financeiro/contas?tipo=Banco&pagina=1&tamanho=20"), Ct))!.Itens.Single().Id.ShouldBe(banco.Id);

        var comSaldo = await client.PostAsJsonAsync(U("/api/financeiro/contas/situacao"), new { ids = new[] { caixa.Id, cofre.Id }, ativo = false }, Ct);
        comSaldo.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await comSaldo.Content.ReadAsStringAsync(Ct)).ShouldContain("Cofre");
        (await Contas(client)).ShouldAllBe(c => c.Ativa);

        var lote = await client.PostAsJsonAsync(U("/api/financeiro/contas/situacao"), new { ids = new[] { caixa.Id, banco.Id }, ativo = false }, Ct);
        lote.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await lote.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("alterados").GetInt32().ShouldBe(2);
        (await client.GetFromJsonAsync<PaginaDeContasDto>(U("/api/financeiro/contas?situacao=inativas&pagina=1&tamanho=20"), Ct))!.Total.ShouldBe(2);
        (await Contas(client)).Count.ShouldBe(3);
        var vendedor = await Entrar(_api, "vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/financeiro/contas/situacao"), new { ids = new[] { caixa.Id }, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var csv = await client.GetStringAsync(U($"/api/relatorios/contas-financeiras.csv?ids={cofre.Id}"), Ct);
        csv.ShouldContain("Cofre");
        csv.ShouldNotContain("Banco");
    }
}
