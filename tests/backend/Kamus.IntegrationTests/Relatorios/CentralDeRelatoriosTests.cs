using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Relatorios;

/// <summary>Central de relatórios: catálogo por perfil, visualização na tela e os relatórios de gestão (vendas agrupadas, curva ABC, sem giro, DRE).</summary>
public sealed class CentralDeRelatoriosTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record ContaDto(Guid Id);
    private sealed record CategoriaDto(Guid Id, string Nome, string Grupo);
    private sealed record OpcaoDto(string Valor, string Rotulo);
    private sealed record ParametroDto(string Nome, string Rotulo, string Tipo, string? Padrao, List<OpcaoDto>? Opcoes);
    private sealed record ItemDoCatalogoDto(string Codigo, string Titulo, string Area, string? Descricao, List<ParametroDto> Parametros);
    private sealed record ColunaDto(string Titulo, string Tipo);
    private sealed record FiltroDto(string Nome, string Valor);
    private sealed record DadosDto(string Titulo, List<ColunaDto> Colunas, List<List<JsonElement>> Linhas, List<FiltroDto> Filtros, int TotalDeLinhas);

    private sealed record Cenario(HttpClient Client, ProdutoDto Camiseta, ProdutoDto Calca, Guid LojaId, FormaDto Pix);

    /// <summary>
    /// Camiseta: 5 Azul M, 5 Azul G, 1 Azul P (custo 20). Vendas: ontem 1 G; hoje 2 M e 1 P, todas no PIX (sem taxa).
    /// Calça: 3 Azul M em estoque, nunca vendida. Despesas do mês: aluguel R$ 1.500 e uma compra de mercadoria lançada à mão (R$ 500, fica fora da DRE).
    /// </summary>
    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var calca = await CriarCamiseta(client, "CAL-001", 30m);
        var loja = (await Lojas(client)).Single();
        await Entrada(client, loja.Id,
            new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 5, custoUnitario = 20m },
            new { skuId = Sku(camiseta, "Azul", "G").Id, quantidade = 5, custoUnitario = 20m },
            new { skuId = Sku(camiseta, "Azul", "P").Id, quantidade = 1, custoUnitario = 20m },
            new { skuId = Sku(calca, "Azul", "M").Id, quantidade = 3, custoUnitario = 30m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = Hoje.AddDays(-30) }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var pix = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!.Single(f => f.Tipo == "Pix");
        (await client.PutAsJsonAsync(U($"/api/vendas/formas/{pix.Id}"), new { pix.Nome, pix.Tipo, contaId = caixa.Id, pix.TaxaAVista, pix.TaxaParcelado, pix.MaxParcelas, pix.PrazoDias }, Ct))
            .EnsureSuccessStatusCode();
        var c = new Cenario(client, camiseta, calca, loja.Id, pix);
        await Vender(c, Hoje.AddDays(-1), ("G", 1));
        await Vender(c, Hoje, ("M", 2));
        await Vender(c, Hoje, ("P", 1));

        (await client.PostAsync(U("/api/financeiro/categorias/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var categorias = (await client.GetFromJsonAsync<List<CategoriaDto>>(U("/api/financeiro/categorias"), Ct))!;
        await Despesa(client, "Aluguel do mês", categorias.Single(x => x.Nome == "Aluguel").Id, 1500m);
        await Despesa(client, "Reposição de estoque", categorias.Single(x => x.Nome == "Compra de mercadorias").Id, 500m);
        return c;
    }

    private static async Task Vender(Cenario c, DateOnly data, params (string Tamanho, int Quantidade)[] itens)
    {
        var r = await c.Client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = c.LojaId,
            data,
            itens = itens.Select(i => new { skuId = Sku(c.Camiseta, "Azul", i.Tamanho).Id, quantidade = i.Quantidade, desconto = 0m }),
            descontoGeral = 0m,
            pagamentos = new[] { new { formaId = c.Pix.Id, valor = itens.Sum(i => i.Quantidade) * 59.90m, parcelas = 1 } },
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
    }

    private static async Task Despesa(HttpClient client, string descricao, Guid categoriaId, decimal valor)
    {
        var r = await client.PostAsJsonAsync(U("/api/financeiro/titulos"),
            new { tipo = "Pagar", descricao, categoriaId, competencia = Hoje, parcelas = new[] { new { vencimento = Hoje.AddDays(5), valor } } }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<DadosDto> Dados(HttpClient client, string codigo, string query = "")
    {
        var r = await client.GetAsync(U($"/api/relatorios/{codigo}/dados{query}"), Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<DadosDto>(Ct))!;
    }

    private static string Periodo => $"?de={Hoje.AddDays(-1):yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}";

    [Fact]
    public async Task Catalogo_MostraOsRelatoriosDoPerfilComAreaEParametros()
    {
        var dono = await Entrar(_api);

        var catalogo = (await dono.GetFromJsonAsync<List<ItemDoCatalogoDto>>(U("/api/relatorios"), Ct))!;

        catalogo.Select(r => r.Codigo).ShouldContain("vendas-agrupadas");
        catalogo.Select(r => r.Codigo).ShouldContain("dre");
        catalogo.Select(r => r.Codigo).ShouldContain("posicao-de-estoque");
        var agrupadas = catalogo.Single(r => r.Codigo == "vendas-agrupadas");
        agrupadas.Area.ShouldBe("Vendas");
        agrupadas.Descricao.ShouldNotBeNullOrWhiteSpace();
        agrupadas.Parametros.Select(p => (p.Nome, p.Tipo)).ShouldBe([("periodo", "Periodo"), ("agrupamento", "Opcao")]);
        agrupadas.Parametros[1].Opcoes!.Select(o => o.Valor).ShouldContain("tamanho");
        catalogo.Single(r => r.Codigo == "dre").Area.ShouldBe("Gestão");
        catalogo.ShouldAllBe(r => r.Area != "" && r.Area != "Outros");
        catalogo.Select(r => r.Codigo).ShouldNotContain("inventario");

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var dela = (await vendedora.GetFromJsonAsync<List<ItemDoCatalogoDto>>(U("/api/relatorios"), Ct))!.Select(r => r.Codigo).ToList();
        dela.ShouldContain("vendas");
        dela.ShouldNotContain("dre");
        dela.ShouldNotContain("curva-abc");
    }

    [Fact]
    public async Task Dados_VendasAgrupadasPorTamanhoECor()
    {
        var c = await Preparar();

        var porTamanho = await Dados(c.Client, "vendas-agrupadas", Periodo + "&agrupamento=tamanho");

        porTamanho.Colunas.Select(x => x.Titulo).ShouldBe(["Tamanho", "Vendas", "Peças", "Total", "% do total", "Custo", "Lucro bruto"]);
        porTamanho.Colunas[3].Tipo.ShouldBe("Moeda");
        porTamanho.Linhas.Select(l => (l[0].GetString(), l[2].GetInt32(), l[3].GetDecimal())).ShouldBe([("M", 2, 119.80m), ("G", 1, 59.90m), ("P", 1, 59.90m)]);
        porTamanho.Linhas[0][4].GetDecimal().ShouldBe(50m);
        porTamanho.Linhas[0][5].GetDecimal().ShouldBe(40m);
        porTamanho.Linhas[0][6].GetDecimal().ShouldBe(79.80m);
        porTamanho.Filtros.ShouldContain(f => f.Nome == "Agrupamento" && f.Valor == "Tamanho");

        var porCor = await Dados(c.Client, "vendas-agrupadas", Periodo + "&agrupamento=cor");
        porCor.Linhas.Single()[0].GetString().ShouldBe("Azul");
        porCor.Linhas.Single()[2].GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Dados_CurvaAbcESemGiro()
    {
        var c = await Preparar();

        var abc = await Dados(c.Client, "curva-abc", Periodo);
        var linha = abc.Linhas.ShouldHaveSingleItem();
        linha[0].GetString().ShouldBe("CAM-001");
        linha[2].GetInt32().ShouldBe(4);
        linha[3].GetDecimal().ShouldBe(239.60m);
        linha[^1].GetString().ShouldBe("A");

        var semGiro = await Dados(c.Client, "produtos-sem-giro", "?dias=30");
        var parada = semGiro.Linhas.ShouldHaveSingleItem();
        parada[0].GetString().ShouldBe("CAL-001");
        parada[2].GetInt32().ShouldBe(3);
        parada[3].GetDecimal().ShouldBe(90m);
        parada[4].ValueKind.ShouldBe(JsonValueKind.Null);
        parada[5].GetString().ShouldBe("Nunca vendido");
    }

    [Fact]
    public async Task Dados_DreGerencial_ReceitaCustoEDespesasSemCompraDeMercadoria()
    {
        var c = await Preparar();

        var dre = await Dados(c.Client, "dre", Periodo);

        string Linha(int i) => dre.Linhas[i][0].GetString()!;
        decimal Valor(string descricao) => dre.Linhas.Single(l => l[0].GetString() == descricao)[1].GetDecimal();
        Valor("Receita bruta de vendas").ShouldBe(239.60m);
        Valor("(−) Taxas de cartão").ShouldBe(0m);
        Valor("(−) Custo das mercadorias vendidas").ShouldBe(-80m);
        Valor("(=) Lucro bruto").ShouldBe(159.60m);
        Valor("Ocupação · Aluguel").ShouldBe(-1500m);
        Valor("(=) Resultado do período").ShouldBe(-1340.40m);
        dre.Linhas.ShouldNotContain(l => l[0].GetString()!.Contains("Compra de mercadorias", StringComparison.Ordinal));
        Linha(0).ShouldBe("Receita bruta de vendas");
        dre.Linhas.Single(l => l[0].GetString() == "(=) Lucro bruto")[2].GetDecimal().ShouldBe(66.6m);
    }

    [Fact]
    public async Task Dados_SemPermissaoOuInexistente()
    {
        var vendedora = await Entrar(_api, "vendedor@kamus.local");

        (await vendedora.GetAsync(U("/api/relatorios/dre/dados"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedora.GetAsync(U("/api/relatorios/nao-existe/dados"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
