using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Atalhos comuns dos testes do estoque: login na empresa, catálogo de exemplo, lojas e entradas.</summary>
internal static class ApiDoEstoque
{
    public const string Senha = "Senha@Teste123";

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Uri U(string caminho) => new(caminho, UriKind.Relative);

    public static KamusApiFactory NovaApi() => new(ambiente: "Development", configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    public sealed record EmpresaDto(Guid Id, string NomeFantasia, string Cnpj);
    public sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    public sealed record GradeDto(Guid Id, string Nome);
    public sealed record CorDto(Guid Id, string Nome);
    public sealed record SkuDto(Guid Id, Guid CorId, string Tamanho, string CodigoDeBarras);
    public sealed record ProdutoDto(Guid Id, string Referencia, List<CorDto> Cores, List<SkuDto> Skus);
    public sealed record LojaDto(Guid Id, string Nome, bool Ativa);
    public sealed record OperacaoDto(Guid OperacaoId, int Itens, int Quantidade);

    public static async Task<HttpClient> Entrar(KamusApiFactory api, string email = "admin@kamus.local", string empresa = "Kamus Moda Centro")
    {
        var client = api.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.NomeFantasia == empresa).Id.ToString());
        return client;
    }

    public static async Task<ProdutoDto> CriarCamiseta(HttpClient client, string referencia = "CAM-001", decimal? custo = 24m)
    {
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var grades = (await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!;
        var cores = (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!;
        var resposta = await client.PostAsJsonAsync(U("/api/produtos"), new
        {
            referencia,
            descricao = $"Camiseta {referencia}",
            precoVenda = 59.90m,
            precoCusto = custo,
            gradeId = grades.Single(g => g.Nome == "PP ao GG").Id,
            corIds = cores.Where(c => c.Nome is "Azul" or "Preto").Select(c => c.Id),
        }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
    }

    public static SkuDto Sku(ProdutoDto p, string cor, string tamanho) =>
        p.Skus.Single(s => s.CorId == p.Cores.Single(c => c.Nome == cor).Id && s.Tamanho == tamanho);

    public static async Task<List<LojaDto>> Lojas(HttpClient client) => (await client.GetFromJsonAsync<List<LojaDto>>(U("/api/estoque/lojas"), Ct))!;

    public static async Task<OperacaoDto> Entrada(HttpClient client, Guid lojaId, params object[] itens)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/estoque/entradas"), new { lojaId, documento = "NF 123", itens }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<OperacaoDto>(Ct))!;
    }
}
