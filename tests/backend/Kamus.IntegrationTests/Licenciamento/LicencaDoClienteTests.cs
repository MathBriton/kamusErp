using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Licenciamento;

/// <summary>Licença do cliente (em memória): situação, bloqueio do suspenso e avisos de vencimento.</summary>
public sealed class LicencaDoClienteTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => CalendarioBrasilia.Hoje(TimeProvider.System);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record LicencaDto(string Plano, string Estado, DateOnly? Vencimento, DateOnly? FimDaCarencia, int? DiasParaVencer, decimal ValorMensal, string Mensagem);
    private sealed record PagamentoDto(decimal Valor, DateOnly PagoEm, DateOnly EstendeuAte);
    private sealed record NotificacaoDto(string Tipo, string Titulo);
    private sealed record ListaDto(List<NotificacaoDto> Itens);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return client;
    }

    /// <summary>Simula o passar do tempo mudando o vencimento da licença do cliente de demonstração.</summary>
    private async Task VencimentoEm(int diasAPartirDeHoje, bool cancelar = false)
    {
        await using var escopo = _factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
        var licenca = await db.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct);
        licenca.RegistrarPagamento(Hoje.AddDays(diasAPartirDeHoje));
        if (cancelar) licenca.Cancelar(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
        escopo.ServiceProvider.GetRequiredService<CacheDeLicencas>().Invalidar(TenantDemo);
    }

    private static async Task<LicencaDto> Licenca(HttpClient client) =>
        (await client.GetFromJsonAsync<LicencaDto>(new Uri("/api/licenca", UriKind.Relative), Ct))!;

    private static Task<HttpResponseMessage> Historico(HttpClient client) =>
        client.GetAsync(new Uri("/api/auditoria", UriKind.Relative), Ct);

    [Fact]
    public async Task ClienteDemo_TemLicencaAtivaNoPlanoPiloto()
    {
        var client = await Entrar();

        var licenca = await Licenca(client);

        licenca.Estado.ShouldBe("Ativa");
        licenca.Plano.ShouldBe("Piloto");
        licenca.Vencimento.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(Hoje);
        licenca.DiasParaVencer.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Carencia_UsoNormalComAvisoEPrazo()
    {
        var client = await Entrar();
        await VencimentoEm(-2);

        (await Historico(client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var licenca = await Licenca(client);
        licenca.Estado.ShouldBe("Carencia");
        licenca.FimDaCarencia.ShouldBe(Hoje.AddDays(3));
        licenca.Mensagem.ShouldContain("carência");
    }

    [Fact]
    public async Task Suspensa_BloqueiaComCodigo402()
    {
        var client = await Entrar();
        await VencimentoEm(-10);

        var resposta = await Historico(client);

        resposta.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problema.GetProperty("codigo").GetString().ShouldBe("LICENCA_SUSPENSA");
        problema.GetProperty("detail").GetString()!.ShouldContain("preservados");
    }

    [Fact]
    public async Task Suspensa_AindaPermiteVerSituacaoNotificacoesExportarDadosERenovarSessao()
    {
        var client = await Entrar();
        await VencimentoEm(-10);

        (await Licenca(client)).Estado.ShouldBe("Suspensa");
        (await client.GetAsync(new Uri("/api/notificacoes", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/api/relatorios/historico-de-alteracoes.csv", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var novo = _factory.CreateClient();
        (await novo.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = Senha }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cancelada_BloqueiaComCodigoProprio()
    {
        var client = await Entrar();
        await VencimentoEm(30, cancelar: true);

        var problema = await (await Historico(client)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        problema.GetProperty("codigo").GetString().ShouldBe("LICENCA_CANCELADA");
    }

    [Fact]
    public async Task Pagamentos_DonoVeOHistorico()
    {
        var client = await Entrar();

        var pagamentos = await client.GetFromJsonAsync<List<PagamentoDto>>(new Uri("/api/licenca/pagamentos", UriKind.Relative), Ct);

        pagamentos.ShouldNotBeNull().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Pagamentos_VendedorNaoVe()
    {
        var client = await Entrar("vendedor@kamus.local");

        (await client.GetAsync(new Uri("/api/licenca/pagamentos", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Vencimento_AvisaSoOsDonosEUmaUnicaVez()
    {
        var admin = await Entrar();
        var vendedor = await Entrar("vendedor@kamus.local");
        await VencimentoEm(3);

        for (var i = 0; i < 2; i++)
        {
            await using var escopo = _factory.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<VerificadorDeVencimentos>().ExecutarAsync(Hoje, Ct);
        }

        var doAdmin = (await admin.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!.Itens;
        doAdmin.Where(n => n.Tipo == "licenca_vencendo").ShouldHaveSingleItem().Titulo.ShouldContain("3 dias");
        (await vendedor.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!.Itens
            .ShouldNotContain(n => n.Tipo.StartsWith("licenca", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Carencia_AvisaQueAMensalidadeVenceu()
    {
        var admin = await Entrar();
        await VencimentoEm(-1);

        await using (var escopo = _factory.Services.CreateAsyncScope())
            await escopo.ServiceProvider.GetRequiredService<VerificadorDeVencimentos>().ExecutarAsync(Hoje, Ct);

        (await admin.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!.Itens
            .ShouldContain(n => n.Tipo == "licenca_em_carencia");
    }
}
