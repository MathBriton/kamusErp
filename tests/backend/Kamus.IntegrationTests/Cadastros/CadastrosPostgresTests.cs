using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Cadastros;

/// <summary>Clientes no PostgreSQL: RLS na tabela e CPF único por cliente do SaaS (índice parcial: vários sem documento).</summary>
[Collection(PostgresGroup.Name)]
public sealed class CadastrosPostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);

    [Fact]
    public async Task Clientes_NoPostgres_DocumentoUnicoERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());

        async Task<HttpStatusCode> Criar(string nome, string? cpf) =>
            (await client.PostAsJsonAsync(new Uri("/api/pessoas", UriKind.Relative), new { tipo = "Fisica", nome, documento = cpf, ehCliente = true }, Ct)).StatusCode;

        (await Criar("Sem CPF 1", null)).ShouldBe(HttpStatusCode.Created);
        (await Criar("Sem CPF 2", null)).ShouldBe(HttpStatusCode.Created);
        (await Criar("Com CPF", "111.444.777-35")).ShouldBe(HttpStatusCode.Created);
        (await Criar("CPF repetido", "111.444.777-35")).ShouldBe(HttpStatusCode.Conflict);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM cadastros.pessoas", conn);
        ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0); // sem app.tenant_id, o RLS não entrega nada
    }

    private sealed record GradeDto(Guid Id, string Nome, List<string> Tamanhos);
    private sealed record CorDto(Guid Id, string Nome);
    private sealed record SkuDto(Guid Id, string Tamanho, string CodigoDeBarras);
    private sealed record ProdutoDto(Guid Id, GradeDto Grade, List<SkuDto> Skus);

    [Fact]
    public async Task Produtos_NoPostgres_GradeSkusECodigoUnico()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());

        (await client.PostAsync(new Uri("/api/grades/padrao", UriKind.Relative), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(new Uri("/api/cores/padrao", UriKind.Relative), null, Ct)).EnsureSuccessStatusCode();
        var grade = (await client.GetFromJsonAsync<List<GradeDto>>(new Uri("/api/grades", UriKind.Relative), Ct))!.Single(g => g.Nome == "Numeração 34 ao 48");
        var cores = (await client.GetFromJsonAsync<List<CorDto>>(new Uri("/api/cores", UriKind.Relative), Ct))!;

        var resposta = await client.PostAsJsonAsync(new Uri("/api/produtos", UriKind.Relative), new
        {
            referencia = "CAL-PG-01",
            descricao = "Calça Jeans Reta",
            precoVenda = 159.90m,
            precoCusto = 64m,
            gradeId = grade.Id,
            corIds = cores.Where(c => c.Nome == "Jeans").Select(c => c.Id),
        }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        var calca = (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
        calca.Grade.Tamanhos.ShouldBe(["34", "36", "38", "40", "42", "44", "46", "48"]);
        calca.Skus.Select(s => s.Tamanho).ShouldBe(calca.Grade.Tamanhos);

        var outro = calca.Skus[1];
        (await client.PutAsJsonAsync(new Uri($"/api/produtos/{calca.Id}/skus/{outro.Id}", UriKind.Relative),
            new { codigoDeBarras = calca.Skus[0].CodigoDeBarras, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
