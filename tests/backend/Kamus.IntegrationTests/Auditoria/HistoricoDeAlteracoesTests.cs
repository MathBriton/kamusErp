using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.Infrastructure.Auditoria;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Identidade.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Auditoria;

/// <summary>Histórico de alterações em memória (sem PostgreSQL), com os dados de demonstração.</summary>
public sealed class HistoricoDeAlteracoesTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id, string Cnpj);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record AlteracaoDto(string Campo, string? Antes, string? Depois);
    private sealed record RegistroDto(Guid Id, DateTimeOffset OcorridoEm, string Tipo, string? Evento, string? Entidade, string? EntidadeId,
        string Descricao, string UsuarioNome, string? Ip, List<AlteracaoDto> Alteracoes);
    private sealed record PaginaDto(List<RegistroDto> Itens, int Total);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<(HttpClient Client, SessaoDto Sessao)> Entrar(string email = "admin@kamus.local", string senha = Senha)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return (client, sessao);
    }

    private static async Task<List<RegistroDto>> Historico(HttpClient client, string filtros = "")
    {
        var pagina = await client.GetFromJsonAsync<PaginaDto>(new Uri($"/api/auditoria?tamanho=200{filtros}", UriKind.Relative), Ct);
        return pagina!.Itens;
    }

    [Fact]
    public async Task DadosDeDemonstracao_GeramInclusoesFeitasPeloSistema()
    {
        var (client, _) = await Entrar();

        var registros = await Historico(client, "&entidade=Empresa");

        var inclusao = registros.Where(r => r.Tipo == "Inclusao" && r.Alteracoes.Any(a => a.Campo == "Cnpj" && a.Depois == "12.345.678/0001-95")).ShouldHaveSingleItem();
        inclusao.UsuarioNome.ShouldBe("Sistema");
        inclusao.Alteracoes.ShouldContain(a => a.Campo == "NomeFantasia" && a.Antes == null && a.Depois == "Kamus Moda Centro");
    }

    [Fact]
    public async Task CamposSensiveis_NuncaSaoGravados()
    {
        var (client, _) = await Entrar();

        var registros = await Historico(client);

        registros.ShouldNotBeEmpty();
        registros.SelectMany(r => r.Alteracoes).ShouldNotContain(a => a.Campo == "SenhaHash" || a.Campo == "TokenHash");
        registros.ShouldNotContain(r => r.Entidade == "RefreshToken");
    }

    [Fact]
    public async Task Login_RegistraEventoComUsuarioEIp()
    {
        var (client, _) = await Entrar();

        var login = (await Historico(client, "&tipo=Evento")).First(r => r.Evento == "login");

        login.UsuarioNome.ShouldBe("Admin Demo");
        login.Descricao.ShouldContain("admin@kamus.local");
    }

    [Fact]
    public async Task SenhaErrada_RegistraTentativaFalha()
    {
        using (var anonimo = _factory.CreateClient())
            await anonimo.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = "errada" }, Ct);
        var (client, _) = await Entrar();

        (await Historico(client, "&tipo=Evento")).ShouldContain(r => r.Evento == "login_falhou" && r.UsuarioNome == "Admin Demo");
    }

    [Fact]
    public async Task Alteracao_RegistraValoresAntesEDepois()
    {
        var (client, sessao) = await Entrar();
        await using (var escopo = _factory.Services.CreateAsyncScope())
        {
            escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(TenantDemo);
            var db = escopo.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
            var empresa = await db.Empresas.SingleAsync(e => e.Id == sessao.Empresas[0].Id, Ct);
            db.Entry(empresa).Property(e => e.NomeFantasia).CurrentValue = "Kamus Moda Centro Novo";
            await db.SaveChangesAsync(Ct);
        }

        var alteracao = (await Historico(client, "&tipo=Alteracao")).ShouldHaveSingleItem();

        alteracao.Entidade.ShouldBe("Empresa");
        alteracao.EntidadeId.ShouldBe(sessao.Empresas[0].Id.ToString());
        alteracao.Alteracoes.ShouldHaveSingleItem().ShouldBe(new AlteracaoDto("NomeFantasia", "Kamus Moda Centro", "Kamus Moda Centro Novo"));
    }

    [Fact]
    public async Task Registros_NaoPodemSerAlteradosNemApagados()
    {
        await Entrar();
        await using var escopo = _factory.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(TenantDemo);
        var db = escopo.ServiceProvider.GetRequiredService<AuditoriaDbContext>();
        var registro = await db.Registros.FirstAsync(Ct);

        db.Registros.Remove(registro);

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Vendedor_NaoAcessaOHistorico()
    {
        var (client, _) = await Entrar("vendedor@kamus.local");

        var resposta = await client.GetAsync(new Uri("/api/auditoria", UriKind.Relative), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SemEmpresaAtiva_Retorna400()
    {
        var (client, _) = await Entrar();
        client.DefaultRequestHeaders.Remove("X-Empresa-Id");

        (await client.GetAsync(new Uri("/api/auditoria", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Historico_VemDoMaisRecenteParaOMaisAntigo()
    {
        var (client, _) = await Entrar();

        var registros = await Historico(client);

        registros.Select(r => r.OcorridoEm).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Relatorio_SoOsRegistrosSelecionados_EPeriodoComFuso()
    {
        var (client, _) = await Entrar();
        var empresas = (await Historico(client, "&entidade=Empresa")).Take(2).ToList();

        var csv = await client.GetStringAsync(new Uri($"/api/relatorios/historico-de-alteracoes.csv?ids={empresas[0].Id}", UriKind.Relative), Ct);
        var hoje = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var doDia = await Historico(client, $"&de={hoje}T00:00:00-03:00&ate={hoje}T23:59:59-03:00");
        var futuro = await Historico(client, "&de=2099-01-01T00:00:00-03:00");

        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(2); // cabeçalho + o selecionado
        doDia.ShouldNotBeEmpty();
        futuro.ShouldBeEmpty();
    }
}
