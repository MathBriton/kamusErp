using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Estoque inicial por planilha: quantidade absoluta por SKU e loja (reimportar corrige, não soma).</summary>
public sealed class EstoqueInicialTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private static readonly string[] Colunas = ["Loja", "Referência", "Cor", "Tamanho", "Código de barras", "Quantidade", "Custo"];

    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record GradeDto(Guid Id, string Nome);
    private sealed record CorDto(Guid Id, string Nome);
    private sealed record SkuDto(Guid Id, Guid CorId, string Tamanho, string CodigoDeBarras);
    private sealed record ProdutoDto(Guid Id, List<CorDto> Cores, List<SkuDto> Skus);
    private sealed record LojaDto(Guid Id, string Nome);
    private sealed record LinhaDto(int Numero, string Acao, string Descricao, List<string> Erros);
    private sealed record ValidacaoDto(Guid Id, int Total, int Validas, int ComErro, int Criar, int Atualizar, List<LinhaDto> Linhas);
    private sealed record ResultadoDto(int Criados, int Atualizados, int Ignorados);
    private sealed record CelulaDto(Guid SkuId, int Quantidade, decimal? CustoMedio);
    private sealed record MatrizDto(List<CelulaDto> Celulas, int Total);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.NomeFantasia == "Kamus Moda Centro").Id.ToString());
        return client;
    }

    private static async Task<ProdutoDto> CriarCamiseta(HttpClient client)
    {
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var grades = (await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!;
        var cores = (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!;
        var resposta = await client.PostAsJsonAsync(U("/api/produtos"), new
        {
            referencia = "CAM-001",
            descricao = "Camiseta Básica",
            precoVenda = 59.90m,
            precoCusto = 24m,
            gradeId = grades.Single(g => g.Nome == "PP ao GG").Id,
            corIds = cores.Where(c => c.Nome is "Azul" or "Preto").Select(c => c.Id),
        }, Ct);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
    }

    private static SkuDto Sku(ProdutoDto p, string cor, string tamanho) =>
        p.Skus.Single(s => s.CorId == p.Cores.Single(c => c.Nome == cor).Id && s.Tamanho == tamanho);

    private static byte[] Planilha(params object?[][] linhas)
    {
        using var livro = new XLWorkbook();
        var aba = livro.Worksheets.Add("Dados");
        for (var c = 0; c < Colunas.Length; c++) aba.Cell(1, c + 1).Value = Colunas[c];
        for (var l = 0; l < linhas.Length; l++)
            for (var c = 0; c < linhas[l].Length; c++)
                aba.Cell(l + 2, c + 1).Value = linhas[l][c] switch { null => Blank.Value, decimal d => (double)d, int i => i, var v => v.ToString() };
        using var ms = new MemoryStream();
        livro.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient client, byte[] arquivo)
    {
        using var form = new MultipartFormDataContent();
        var conteudo = new ByteArrayContent(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(conteudo, "arquivo", "estoque.xlsx");
        return await client.PostAsync(U("/api/importacoes/estoque-inicial/validar"), form, Ct);
    }

    private static async Task<ValidacaoDto> Validar(HttpClient client, byte[] arquivo)
    {
        var resposta = await Enviar(client, arquivo);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<ValidacaoDto>(Ct))!;
    }

    private static async Task<ResultadoDto> Confirmar(HttpClient client, Guid id)
    {
        var resposta = await client.PostAsync(U($"/api/importacoes/{id}/confirmar"), null, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<ResultadoDto>(Ct))!;
    }

    private static async Task<MatrizDto> Matriz(HttpClient client, Guid produtoId) =>
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produtoId}"), Ct))!;

    [Fact]
    public async Task Modelo_TemAsColunasDoEstoqueInicial()
    {
        var client = await Entrar();

        var resposta = await client.GetAsync(U("/api/importacoes/estoque-inicial/modelo.xlsx"), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var livro = new XLWorkbook(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync(Ct)));
        var cabecalho = livro.Worksheet("Dados").Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        cabecalho.ShouldBe(Colunas);
    }

    [Fact]
    public async Task Importar_PorReferenciaOuCodigo_ValoresAbsolutos()
    {
        var client = await Entrar();
        var camiseta = await CriarCamiseta(client);
        var lojas = (await client.GetFromJsonAsync<List<LojaDto>>(U("/api/estoque/lojas"), Ct))!;
        var pretoG = Sku(camiseta, "Preto", "G");

        var validacao = await Validar(client, Planilha(
            [null, "cam-001", "azul", "m", null, 10, 20m],
            [lojas[0].Nome, null, null, null, pretoG.CodigoDeBarras, 3, null],
            [null, "CAM-001", "Verde", "M", null, 1, null],
            [null, "CAM-001", "Azul", "M", null, 2, null],
            [null, "CAM-001", "Azul", "P", null, -1, null],
            [null, "CAM-001", "Azul", "G", null, 1.5m, null],
            ["Loja Inexistente", "CAM-001", "Azul", "GG", null, 1, null],
            [null, "CAM-001", "Azul", "PP", null, null, null]));

        validacao.ShouldBe(validacao with { Total = 8, Validas = 2, ComErro = 6, Criar = 2 });
        validacao.Linhas.Single(l => l.Numero == 4).Erros.ShouldContain(e => e.Contains("Verde"));
        validacao.Linhas.Single(l => l.Numero == 5).Erros.ShouldContain(e => e.Contains("repetid", StringComparison.OrdinalIgnoreCase));
        validacao.Linhas.Single(l => l.Numero == 8).Erros.ShouldContain(e => e.Contains("Loja Inexistente"));
        (await Matriz(client, camiseta.Id)).Total.ShouldBe(0);

        (await Confirmar(client, validacao.Id)).ShouldBe(new ResultadoDto(2, 0, 6));
        var matriz = await Matriz(client, camiseta.Id);
        matriz.Celulas.Single(c => c.SkuId == Sku(camiseta, "Azul", "M").Id).ShouldBe(new CelulaDto(Sku(camiseta, "Azul", "M").Id, 10, 20m));
        matriz.Celulas.Single(c => c.SkuId == pretoG.Id).ShouldBe(new CelulaDto(pretoG.Id, 3, 24m)); // sem custo: custo do produto

        var correcao = await Validar(client, Planilha([null, "CAM-001", "Azul", "M", null, 7, null]));
        correcao.Atualizar.ShouldBe(1);
        (await Confirmar(client, correcao.Id)).ShouldBe(new ResultadoDto(0, 1, 0));
        var corrigida = (await Matriz(client, camiseta.Id)).Celulas.Single(c => c.SkuId == Sku(camiseta, "Azul", "M").Id);
        corrigida.ShouldBe(corrigida with { Quantidade = 7, CustoMedio = 20m });
    }

    [Fact]
    public async Task VariasLojas_ColunaLojaObrigatoria()
    {
        var client = await Entrar();
        await CriarCamiseta(client);
        await client.GetAsync(U("/api/estoque/lojas"), Ct);
        (await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = "Depósito" }, Ct)).EnsureSuccessStatusCode();

        var validacao = await Validar(client, Planilha([null, "CAM-001", "Azul", "M", null, 1, null], ["depósito", "CAM-001", "Azul", "M", null, 2, null]));

        validacao.Linhas[0].Erros.ShouldContain(e => e.Contains("loja", StringComparison.OrdinalIgnoreCase));
        validacao.Linhas[1].Acao.ShouldBe("Criar");
    }

    [Fact]
    public async Task Vendedor_NaoImportaEstoque()
    {
        var vendedor = await Entrar("vendedor@kamus.local");

        (await vendedor.GetAsync(U("/api/importacoes/estoque-inicial/modelo.xlsx"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
