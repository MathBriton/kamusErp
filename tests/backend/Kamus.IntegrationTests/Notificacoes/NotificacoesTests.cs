using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Kamus.IntegrationTests.Notificacoes;

/// <summary>Notificações por usuário, em memória, com entrega em tempo real via SignalR.</summary>
public sealed class NotificacoesTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record SessaoDto(string AccessToken);
    private sealed record NotificacaoDto(Guid Id, string Tipo, string Titulo, string Mensagem, string? Link, DateTimeOffset CriadaEm, DateTimeOffset? LidaEm);
    private sealed record ListaDto(List<NotificacaoDto> Itens, int NaoLidas);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<(HttpClient Client, string Token)> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var token = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private static async Task<ListaDto> Listar(HttpClient client) =>
        (await client.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!;

    [Fact]
    public async Task UsuarioDeTeste_TemNotificacoesDeBoasVindasNaoLidas()
    {
        var (client, _) = await Entrar();

        var lista = await Listar(client);

        lista.Itens.ShouldContain(n => n.Titulo == "Bem-vindo ao Kamus");
        lista.NaoLidas.ShouldBe(lista.Itens.Count(n => n.LidaEm == null));
        lista.NaoLidas.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MarcarComoLida_DiminuiAContagem()
    {
        var (client, _) = await Entrar();
        var antes = await Listar(client);
        var alvo = antes.Itens.First(n => n.LidaEm == null);

        (await client.PostAsync(new Uri($"/api/notificacoes/{alvo.Id}/lida", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var depois = await Listar(client);
        depois.NaoLidas.ShouldBe(antes.NaoLidas - 1);
        depois.Itens.Single(n => n.Id == alvo.Id).LidaEm.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarcarTodasComoLidas_ZeraAContagem()
    {
        var (client, _) = await Entrar();

        (await client.PostAsync(new Uri("/api/notificacoes/lidas", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Listar(client)).NaoLidas.ShouldBe(0);
    }

    [Fact]
    public async Task UsuarioNaoVeNemAlteraNotificacoesDeOutro()
    {
        var (admin, _) = await Entrar();
        var doAdmin = (await Listar(admin)).Itens.First();
        var (vendedor, _) = await Entrar("vendedor@kamus.local");

        (await Listar(vendedor)).Itens.ShouldNotContain(n => n.Id == doAdmin.Id);
        (await vendedor.PostAsync(new Uri($"/api/notificacoes/{doAdmin.Id}/lida", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SemLogin_Retorna401()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync(new Uri("/api/notificacoes", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NotificacaoNova_ChegaEmTempoRealPeloSignalR()
    {
        var (client, token) = await Entrar();
        var recebida = new TaskCompletionSource<NotificacaoDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var conexao = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/notificacoes"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        conexao.On<NotificacaoDto>("notificacao", n => recebida.TrySetResult(n));
        await conexao.StartAsync(Ct);

        (await client.PostAsync(new Uri("/api/notificacoes/teste", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var notificacao = await recebida.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        notificacao.Titulo.ShouldBe("Notificação de teste");
    }

    [Fact]
    public async Task NotificacaoDeTeste_ForaDeDevelopment_NaoExiste()
    {
        await using var producao = new KamusApiFactory(ambiente: "Production");
        using var client = producao.CreateClient();

        (await client.PostAsync(new Uri("/api/notificacoes/teste", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BloqueioDeUsuario_AvisaOsDonos()
    {
        using (var anonimo = _factory.CreateClient())
            for (var i = 0; i < 5; i++)
                await anonimo.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "vendedor@kamus.local", senha = "errada" }, Ct);
        var (admin, _) = await Entrar();

        var alerta = (await Listar(admin)).Itens.Where(n => n.Tipo == "usuario_bloqueado").ShouldHaveSingleItem();
        alerta.Mensagem.ShouldContain("vendedor@kamus.local");
        alerta.LidaEm.ShouldBeNull();
    }
}
