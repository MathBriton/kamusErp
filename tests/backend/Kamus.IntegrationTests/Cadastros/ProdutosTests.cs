using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.IntegrationTests.Infra;
using Kamus.SharedKernel.Catalogo;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Cadastros;

/// <summary>Grades, cores e produtos com SKUs (cor × tamanho) e códigos de barras.</summary>
public sealed class ProdutosTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private static readonly string[] PpAoGg = ["PP", "P", "M", "G", "GG"];
    private static readonly string[] SoPeM = ["P", "M"];
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record GradeDto(Guid Id, string Nome, List<string> Tamanhos, bool EmUso);
    private sealed record CorDto(Guid Id, string Nome, string? Hex);
    private sealed record SkuDto(Guid Id, Guid CorId, string Tamanho, string CodigoDeBarras, decimal? PrecoVenda, decimal Preco, bool Ativo);
    private sealed record ProdutoDto(Guid Id, string Referencia, string Descricao, decimal PrecoVenda, decimal? Markup, GradeDto Grade, List<CorDto> Cores, List<SkuDto> Skus, bool Ativo);
    private sealed record ResumoDto(Guid Id, string Referencia, string Descricao, int Skus, List<string> Cores);
    private sealed record PaginaDto(List<ResumoDto> Itens, int Total);
    private sealed record PorCodigoDto(ProdutoDto Produto, SkuDto Sku, string Cor);
    private sealed record SugestoesDto(List<string> Marcas, List<string> Categorias, List<string> Colecoes);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return client;
    }

    private static async Task<(List<GradeDto> Grades, List<CorDto> Cores)> Padroes(HttpClient client)
    {
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();
        return ((await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!, (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!);
    }

    private static async Task<ProdutoDto> CriarCamiseta(HttpClient client, string referencia = "CAM-001")
    {
        var (grades, cores) = await Padroes(client);
        var resposta = await client.PostAsJsonAsync(U("/api/produtos"), new
        {
            referencia,
            descricao = "Camiseta Básica Gola V",
            marca = "Kamus",
            categoria = "Camisetas",
            colecao = "Verão 2027",
            precoVenda = 59.90m,
            precoCusto = 24m,
            gradeId = grades.Single(g => g.Nome == "PP ao GG").Id,
            corIds = cores.Where(c => c.Nome is "Azul" or "Preto").Select(c => c.Id),
        }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
    }

    [Fact]
    public async Task Padroes_CriaGradesECoresUmaVezSo()
    {
        var client = await Entrar();

        var (grades, cores) = await Padroes(client);
        var (grades2, cores2) = await Padroes(client);

        grades.ShouldContain(g => g.Nome == "PP ao GG" && g.Tamanhos.SequenceEqual(PpAoGg));
        cores.ShouldContain(c => c.Nome == "Azul" && c.Hex == "#1D4ED8");
        grades2.Count.ShouldBe(grades.Count);
        cores2.Count.ShouldBe(cores.Count);
    }

    [Fact]
    public async Task CriarProduto_GeraSkusDaGradeComCodigosInternos()
    {
        var client = await Entrar();

        var camiseta = await CriarCamiseta(client);

        camiseta.Skus.Count.ShouldBe(10);
        camiseta.Markup.ShouldBe(2.50m);
        camiseta.Grade.Tamanhos.ShouldBe(["PP", "P", "M", "G", "GG"]);
        camiseta.Skus.Select(s => s.CodigoDeBarras).Distinct().Count().ShouldBe(10);
        camiseta.Skus.ShouldAllBe(s => s.CodigoDeBarras.StartsWith('2') && s.Preco == 59.90m);
    }

    [Fact]
    public async Task CodigosInternos_ContinuamASequenciaEntreProdutos()
    {
        var client = await Entrar();
        var primeiro = await CriarCamiseta(client, "CAM-001");
        var segundo = await CriarCamiseta(client, "CAM-002");

        primeiro.Skus.Select(s => s.CodigoDeBarras).Intersect(segundo.Skus.Select(s => s.CodigoDeBarras)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Criar_ReferenciaRepetida409_DadosInvalidos400()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);

        (await client.PostAsJsonAsync(U("/api/produtos"), new
        {
            referencia = "cam-001",
            descricao = "Outra",
            precoVenda = 10m,
            gradeId = camiseta.Grade.Id,
            corIds = camiseta.Cores.Select(c => c.Id),
        }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync(U("/api/produtos"), new { referencia = "X", descricao = "", precoVenda = -1m, gradeId = camiseta.Grade.Id, corIds = Array.Empty<Guid>() }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Criar_SemGrade400ComMensagem()
    {
        var client = await Entrar();
        var (_, cores) = await Padroes(client);

        var semGrade = await client.PostAsJsonAsync(U("/api/produtos"),
            new { referencia = "SG-1", descricao = "Sem grade", precoVenda = 10m, gradeId = (Guid?)null, corIds = new[] { cores[0].Id } }, Ct);

        semGrade.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Detalhe(semGrade)).ShouldBe("Escolha uma grade de tamanhos.");
    }

    [Fact]
    public async Task Criar_CampoEmFormatoInvalido400ComMensagemEmPortugues()
    {
        var client = await Entrar();

        using var corpo = new StringContent("""{"referencia":"X","descricao":"Y","precoVenda":10,"gradeId":"","corIds":[]}""", System.Text.Encoding.UTF8, "application/json");
        var resposta = await client.PostAsync(U("/api/produtos"), corpo, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Detalhe(resposta)).ShouldBe("Os dados enviados estão em formato inválido. Confira os campos e tente de novo.");
    }

    private static async Task<string?> Detalhe(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("detail").GetString();

    [Fact]
    public async Task AlterarSku_EanDoFornecedorPrecoProprioEDesativar()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var gg = camiseta.Skus.First(s => s.Tamanho == "GG");

        var resposta = await client.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}/skus/{gg.Id}"), new { codigoDeBarras = "7891000315507", precoVenda = 69.90m, ativo = true }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<SkuDto>(Ct))!.Preco.ShouldBe(69.90m);

        (await client.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}/skus/{gg.Id}"), new { codigoDeBarras = "7891000315508", ativo = true }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var outro = camiseta.Skus.First(s => s.Tamanho == "PP");
        (await client.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}/skus/{outro.Id}"), new { codigoDeBarras = "7891000315507", ativo = true }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PorCodigoDeBarras_EncontraOSku()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var sku = camiseta.Skus.First(s => s.Tamanho == "M");

        var achado = await client.GetFromJsonAsync<PorCodigoDto>(U($"/api/produtos/por-codigo/{sku.CodigoDeBarras}"), Ct);

        achado!.Sku.Id.ShouldBe(sku.Id);
        achado.Produto.Referencia.ShouldBe("CAM-001");
        (await client.GetAsync(U("/api/produtos/por-codigo/7891000315507"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Editar_AdicionaCorEDesativaARetirada()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var cores = (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!;
        var azul = cores.Single(c => c.Nome == "Azul");
        var vermelho = cores.Single(c => c.Nome == "Vermelho");

        var resposta = await client.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}"), new
        {
            descricao = "Camiseta Gola V",
            precoVenda = 64.90m,
            precoCusto = 24m,
            corIds = new[] { azul.Id, vermelho.Id },
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var editado = (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
        editado.Descricao.ShouldBe("Camiseta Gola V");
        editado.Skus.Count.ShouldBe(15);
        editado.Skus.Count(s => s.Ativo).ShouldBe(10);
    }

    [Fact]
    public async Task Listar_BuscaPorDescricaoReferenciaOuCodigo()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);

        var porNome = (await client.GetFromJsonAsync<PaginaDto>(U("/api/produtos?busca=gola%20v"), Ct))!;
        var porEan = (await client.GetFromJsonAsync<PaginaDto>(U($"/api/produtos?busca={camiseta.Skus[0].CodigoDeBarras}"), Ct))!;
        var nada = (await client.GetFromJsonAsync<PaginaDto>(U("/api/produtos?busca=calca"), Ct))!;

        porNome.Itens.ShouldHaveSingleItem().Skus.ShouldBe(10);
        porNome.Itens[0].Cores.ShouldBe(["Azul", "Preto"], ignoreOrder: true);
        porEan.Total.ShouldBe(1);
        nada.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Grade_EmUsoNaoPodeSerAlterada()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);

        var resposta = await client.PutAsJsonAsync(U($"/api/grades/{camiseta.Grade.Id}"), new { nome = "PP ao GG", tamanhos = SoPeM }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!.Single(g => g.Id == camiseta.Grade.Id).EmUso.ShouldBeTrue();
    }

    [Fact]
    public async Task Vendedor_ConsultaMasNaoCadastra()
    {
        var admin = await Entrar();
        var camiseta = await CriarCamiseta(admin);
        var vendedor = await Entrar("vendedor@kamus.local");

        (await vendedor.GetAsync(U($"/api/produtos/{camiseta.Id}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await vendedor.PostAsync(U("/api/cores/padrao"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}"), new { descricao = "X", precoVenda = 1m, corIds = camiseta.Cores.Select(c => c.Id) }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sugestoes_MarcasCategoriasColecoes()
    {
        var client = await Entrar();
        await CriarCamiseta(client);

        var sugestoes = (await client.GetFromJsonAsync<SugestoesDto>(U("/api/produtos/sugestoes"), Ct))!;

        sugestoes.Marcas.ShouldBe(["Kamus"]);
        sugestoes.Categorias.ShouldBe(["Camisetas"]);
        sugestoes.Colecoes.ShouldBe(["Verão 2027"]);
    }

    [Fact]
    public async Task PesquisaGlobalERelatorio()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);

        var busca = await client.GetFromJsonAsync<JsonElement>(U($"/api/busca?q={camiseta.Skus[0].CodigoDeBarras}"), Ct);
        busca.GetProperty("grupos").EnumerateArray().ShouldContain(g => g.GetProperty("grupo").GetString() == "Produtos");

        var csv = await client.GetStringAsync(U("/api/relatorios/produtos.csv"), Ct);
        csv.ShouldContain("CAM-001");
        csv.ShouldContain(camiseta.Skus[0].CodigoDeBarras);
    }

    [Fact]
    public async Task Relatorio_SoOsSelecionados()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var bermuda = await CriarCamiseta(client, "BER-001");

        var csv = await client.GetStringAsync(U($"/api/relatorios/produtos.csv?ids={bermuda.Id}"), Ct);

        csv.ShouldContain("BER-001");
        csv.ShouldNotContain(camiseta.Referencia);
        var dados = await client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/produtos/dados?ids={bermuda.Id},{Guid.NewGuid()}"), Ct);
        dados.GetProperty("filtros")[0].GetProperty("valor").GetString().ShouldBe("1 produto");
    }

    [Fact]
    public async Task RelatoriosDeGradesECores_TodasOuSoAsSelecionadas()
    {
        var client = await Entrar();
        var (grades, cores) = await Padroes(client);
        var ppAoGg = grades.Single(g => g.Nome == "PP ao GG");
        var azul = cores.Single(c => c.Nome == "Azul");

        var todas = await client.GetStringAsync(U("/api/relatorios/grades.csv"), Ct);
        todas.ShouldContain("PP ao GG");
        todas.ShouldContain("Único");
        var soUma = await client.GetStringAsync(U($"/api/relatorios/grades.csv?ids={ppAoGg.Id}"), Ct);
        soUma.ShouldContain("PP, P, M, G, GG");
        soUma.ShouldNotContain("Único");

        var corCsv = await client.GetStringAsync(U($"/api/relatorios/cores.csv?ids={azul.Id}"), Ct);
        corCsv.ShouldContain("Azul");
        corCsv.ShouldNotContain("Preto");
        (await client.GetStringAsync(U("/api/relatorios/cores.csv?busca=pre"), Ct)).ShouldContain("Preto");
    }

    [Fact]
    public async Task SituacaoEmLote_InativaEReativaVarios_VendedorNao()
    {
        var client = await Entrar();
        var a = await CriarCamiseta(client);
        var b = await CriarCamiseta(client, "CAM-002");

        var inativar = await client.PostAsJsonAsync(U("/api/produtos/situacao"), new { ids = new[] { a.Id, b.Id }, ativo = false }, Ct);
        inativar.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await inativar.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("alterados").GetInt32().ShouldBe(2);
        (await client.GetFromJsonAsync<PaginaDto>(U("/api/produtos?situacao=inativos"), Ct))!.Total.ShouldBe(2);

        (await client.PostAsJsonAsync(U("/api/produtos/situacao"), new { ids = new[] { a.Id }, ativo = true }, Ct)).EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<PaginaDto>(U("/api/produtos?situacao=ativos"), Ct))!.Itens.Single().Id.ShouldBe(a.Id);
        (await client.PostAsJsonAsync(U("/api/produtos/situacao"), new { ids = Array.Empty<Guid>(), ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var vendedor = await Entrar("vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/produtos/situacao"), new { ids = new[] { a.Id }, ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CatalogoDeProdutos_ExpoeProdutoESkusParaOutrosModulos()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var azulM = camiseta.Skus.Single(s => s.CorId == camiseta.Cores.Single(c => c.Nome == "Azul").Id && s.Tamanho == "M");

        await using var escopo = _factory.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(TenantDemo);
        var catalogo = escopo.ServiceProvider.GetRequiredService<ICatalogoDeProdutos>();

        var produto = (await catalogo.ProdutoAsync(camiseta.Id, Ct))!;
        produto.Tamanhos.ShouldBe(PpAoGg);
        produto.Cores.Select(c => c.Nome).ShouldBe(["Azul", "Preto"], ignoreOrder: true);
        produto.Skus.Count.ShouldBe(10);
        produto.PrecoCusto.ShouldBe(24m);

        var sku = (await catalogo.SkusAsync([azulM.Id], Ct)).Single();
        sku.ShouldBe(sku with { ProdutoId = camiseta.Id, Referencia = "CAM-001", Descricao = "Camiseta Básica Gola V", Cor = "Azul", Tamanho = "M", CodigoDeBarras = azulM.CodigoDeBarras, PrecoCusto = 24m, Ativo = true });

        (await catalogo.TodosOsSkusAsync(Ct)).ShouldContain(s => s.Id == azulM.Id);
        (await catalogo.SkuPorCodigoAsync($" {azulM.CodigoDeBarras} ", Ct))!.Id.ShouldBe(azulM.Id);
        (await catalogo.SkuPorCodigoAsync("7891234567895", Ct)).ShouldBeNull();
        (await catalogo.ProdutoAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task CatalogoDeProdutos_SemTenant_NaoEnxergaNada()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);

        await using var escopo = _factory.Services.CreateAsyncScope();
        var catalogo = escopo.ServiceProvider.GetRequiredService<ICatalogoDeProdutos>();

        (await catalogo.ProdutoAsync(camiseta.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task DiretorioDeFornecedores_AchaPeloCnpjECadastraSeNaoExiste()
    {
        var client = await Entrar();
        (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Juridica", nome = "Cliente PJ LTDA", documento = "11.444.777/0001-61", ehCliente = true }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var escopo = _factory.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(TenantDemo);
        var diretorio = escopo.ServiceProvider.GetRequiredService<IDiretorioDeFornecedores>();

        (await diretorio.PorDocumentoAsync("11222333000181", Ct)).ShouldBeNull();
        var novo = await diretorio.GarantirAsync(new NovoFornecedor("11222333000181", "Confecções Aurora LTDA", "Aurora Moda", "1133334444", "03016000",
            "Rua Oriente", "100", "Brás", "São Paulo", "SP"), Ct);
        novo.Nome.ShouldBe("Confecções Aurora LTDA");
        (await diretorio.GarantirAsync(new NovoFornecedor("11222333000181", "Outro nome", null, null, null, null, null, null, null, null), Ct)).Id.ShouldBe(novo.Id);
        (await diretorio.PorDocumentoAsync("11.222.333/0001-81", Ct))!.Id.ShouldBe(novo.Id);

        var clienteQueVirouFornecedor = await diretorio.GarantirAsync(new NovoFornecedor("11444777000161", "Cliente PJ LTDA", null, null, null, null, null, null, null, null), Ct);
        var pessoa = await client.GetFromJsonAsync<JsonElement>(U($"/api/pessoas/{clienteQueVirouFornecedor.Id}"), Ct);
        pessoa.GetProperty("ehCliente").GetBoolean().ShouldBeTrue();
        pessoa.GetProperty("ehFornecedor").GetBoolean().ShouldBeTrue();
        var fornecedor = await client.GetFromJsonAsync<JsonElement>(U($"/api/pessoas/{novo.Id}"), Ct);
        fornecedor.GetProperty("endereco").GetProperty("cidade").GetString().ShouldBe("São Paulo");
        fornecedor.GetProperty("telefone").GetString().ShouldBe("1133334444");
    }
}
