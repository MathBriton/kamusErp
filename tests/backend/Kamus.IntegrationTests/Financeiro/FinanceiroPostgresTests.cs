using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Financeiro;

/// <summary>Financeiro no PostgreSQL: título com parcelas, baixa, estorno, transferência, fluxo e RLS nas tabelas do schema financeiro.</summary>
[Collection(PostgresGroup.Name)]
public sealed class FinanceiroPostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record ContaDto(Guid Id, decimal Saldo);
    private sealed record BaixaDto(Guid Id);
    private sealed record ParcelaDto(Guid Id, decimal Saldo, List<BaixaDto> Baixas);
    private sealed record TituloDto(Guid Id, List<ParcelaDto> Parcelas);
    private sealed record FluxoDto(decimal SaldoInicial, decimal SaldoFinal);

    [Fact]
    public async Task Financeiro_NoPostgres_BaixaEstornoTransferenciaFluxoERls()
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
        var hoje = Kamus.SharedKernel.Tempo.CalendarioBrasilia.Hoje(TimeProvider.System);
        var sufixo = Guid.NewGuid().ToString()[..6];

        (await client.PostAsync(U("/api/financeiro/categorias/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = $"Caixa {sufixo}", tipo = "Caixa", saldoInicial = 1000m, saldoInicialEm = hoje }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        var banco = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = $"Banco {sufixo}", tipo = "Banco", saldoInicial = 0m, saldoInicialEm = hoje }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        var criado = await client.PostAsJsonAsync(U("/api/financeiro/titulos"), new
        {
            tipo = "Pagar",
            descricao = $"Aluguel {sufixo}",
            competencia = hoje,
            repetirMeses = 2,
            parcelas = new[] { new { vencimento = hoje, valor = 300m } },
        }, Ct);
        criado.StatusCode.ShouldBe(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync(Ct));
        var titulo = (await criado.Content.ReadFromJsonAsync<TituloDto>(Ct))!;

        var baixa = await client.PostAsJsonAsync(U($"/api/financeiro/parcelas/{titulo.Parcelas[0].Id}/baixas"), new { data = hoje, contaId = caixa.Id, valor = 300m, juros = 0m, multa = 0m, desconto = 0m }, Ct);
        baixa.StatusCode.ShouldBe(HttpStatusCode.OK, await baixa.Content.ReadAsStringAsync(Ct));
        var baixaId = (await baixa.Content.ReadFromJsonAsync<TituloDto>(Ct))!.Parcelas[0].Baixas.Single().Id;
        (await client.PostAsync(U($"/api/financeiro/baixas/{baixaId}/estornar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U($"/api/financeiro/parcelas/{titulo.Parcelas[0].Id}/baixas"), new { data = hoje, contaId = caixa.Id, valor = 300m, juros = 0m, multa = 0m, desconto = 0m }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/financeiro/transferencias"), new { origemId = caixa.Id, destinoId = banco.Id, data = hoje, valor = 200m }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var contas = (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!;
        contas.Single(c => c.Id == caixa.Id).Saldo.ShouldBe(500m);
        contas.Single(c => c.Id == banco.Id).Saldo.ShouldBe(200m);
        var fluxo = (await client.GetFromJsonAsync<FluxoDto>(U($"/api/financeiro/fluxo?de={hoje:yyyy-MM-dd}&ate={hoje.AddDays(40):yyyy-MM-dd}&contaId={caixa.Id}"), Ct))!;
        (fluxo.SaldoInicial, fluxo.SaldoFinal).ShouldBe((1000m, 500m));

        (await client.PutAsJsonAsync(U($"/api/financeiro/crediario/clientes/{Guid.NewGuid()}/limite"), new { clienteNome = $"Cliente {sufixo}", limite = 300m, bloqueado = false }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(U("/api/financeiro/crediario/configuracao"), new { multaPercentual = 2m, jurosMensalPercentual = 1m, diasParaBloquear = 15 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(U("/api/financeiro/crediario/carteira"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        foreach (var tabela in new[] { "contas", "categorias", "titulos", "parcelas", "baixas", "movimentos", "limites_de_credito", "configuracoes_do_crediario" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM financeiro.{tabela}", conn);
            ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0, $"RLS em financeiro.{tabela}");
        }
    }
}
