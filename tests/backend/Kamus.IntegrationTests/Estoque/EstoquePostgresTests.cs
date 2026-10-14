using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Estoque no PostgreSQL: entrada, transferência, kardex ordenado e RLS nas tabelas do schema estoque.</summary>
[Collection(PostgresGroup.Name)]
public sealed class EstoquePostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record GradeDto(Guid Id, string Nome);
    private sealed record CorDto(Guid Id, string Nome);
    private sealed record SkuDto(Guid Id, string Tamanho);
    private sealed record ProdutoDto(Guid Id, List<SkuDto> Skus);
    private sealed record LojaDto(Guid Id, string Nome);
    private sealed record CelulaDto(Guid SkuId, int Quantidade, decimal? CustoMedio);
    private sealed record MatrizDto(List<CelulaDto> Celulas, int Total);
    private sealed record MovimentoDto(string Tipo, int Quantidade, int SaldoApos);

    [Fact]
    public async Task Estoque_NoPostgres_EntradaTransferenciaKardexERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(U("/api/auth/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());

        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var grade = (await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!.Single(g => g.Nome == "PP ao GG");
        var cor = (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!.Single(c => c.Nome == "Branco");
        var referencia = $"EST-{Guid.NewGuid().ToString()[..6]}";
        var produto = (await (await client.PostAsJsonAsync(U("/api/produtos"),
            new { referencia, descricao = "Blusa", precoVenda = 79.90m, precoCusto = 30m, gradeId = grade.Id, corIds = new[] { cor.Id } }, Ct))
            .Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
        var m = produto.Skus.Single(s => s.Tamanho == "M").Id;

        var principal = (await client.GetFromJsonAsync<List<LojaDto>>(U("/api/estoque/lojas"), Ct))![0];
        var nova = await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = $"Depósito {referencia}" }, Ct);
        nova.StatusCode.ShouldBe(HttpStatusCode.Created);
        var deposito = (await nova.Content.ReadFromJsonAsync<LojaDto>(Ct))!;

        (await client.PostAsJsonAsync(U("/api/estoque/entradas"), new { lojaId = principal.Id, itens = new[] { new { skuId = m, quantidade = 6, custoUnitario = 31.5m } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/estoque/transferencias"),
            new { origemId = principal.Id, destinoId = deposito.Id, itens = new[] { new { skuId = m, quantidade = 2 } } }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = deposito.Id, motivo = "Furto", itens = new[] { new { skuId = m, quantidade = -3 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var noDeposito = (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produto.Id}?lojaId={deposito.Id}"), Ct))!;
        noDeposito.Celulas.Single(c => c.SkuId == m).ShouldBe(new CelulaDto(m, 2, 31.5m));
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produto.Id}"), Ct))!.Total.ShouldBe(6);
        var kardex = (await client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={m}"), Ct))!;
        kardex.ShouldBe([new("TransferenciaEntrada", 2, 2), new("TransferenciaSaida", -2, 4), new("Entrada", 6, 6)]);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        foreach (var tabela in new[] { "lojas", "saldos", "movimentos" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM estoque.{tabela}", conn);
            ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0, $"RLS em estoque.{tabela}");
        }
    }
}
