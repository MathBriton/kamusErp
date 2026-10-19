using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Notificacoes.Aplicacao;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Notificacoes;

/// <summary>Aparelhos do app mobile: cada notificação do sininho também sai como push para os aparelhos do usuário.</summary>
public sealed class PushTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private const string TokenDoCelular = "ExponentPushToken[abc123]";

    private readonly EnvioDePushFalso _push = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public PushTests() =>
        _factory = new KamusApiFactory(ambiente: "Development", configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha })
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IEnvioDePush>(_push)));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record SessaoDto(string AccessToken);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private sealed class EnvioDePushFalso : IEnvioDePush
    {
        public List<MensagemDePush> Enviadas { get; } = [];
        public HashSet<string> Invalidos { get; } = [];

        public Task<IReadOnlyList<string>> EnviarAsync(MensagemDePush mensagem, CancellationToken ct)
        {
            Enviadas.Add(mensagem);
            return Task.FromResult<IReadOnlyList<string>>(mensagem.Tokens.Where(Invalidos.Contains).ToList());
        }
    }

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var r = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> Registrar(HttpClient client, string token) =>
        client.PutAsJsonAsync(U("/api/notificacoes/dispositivos"), new { token, plataforma = "android" }, Ct);

    [Fact]
    public async Task Notificacao_ComAparelhoRegistrado_SaiTambemComoPush()
    {
        var client = await Entrar();
        (await Registrar(client, TokenDoCelular)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsync(U("/api/notificacoes/teste"), null, Ct)).EnsureSuccessStatusCode();

        var push = _push.Enviadas.ShouldHaveSingleItem();
        push.Tokens.ShouldBe([TokenDoCelular]);
        push.Titulo.ShouldBe("Notificação de teste");
    }

    [Fact]
    public async Task Notificacao_DeOutroUsuario_NaoVaiParaOMeuAparelho()
    {
        var vendedora = await Entrar("vendedor@kamus.local");
        (await Registrar(vendedora, TokenDoCelular)).EnsureSuccessStatusCode();
        var dono = await Entrar();

        (await dono.PostAsync(U("/api/notificacoes/teste"), null, Ct)).EnsureSuccessStatusCode();

        _push.Enviadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispositivo_TokenInvalidoRecusado_RemovidoNaoRecebeMais()
    {
        var client = await Entrar();
        (await Registrar(client, "qualquer-coisa")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Registrar(client, TokenDoCelular)).EnsureSuccessStatusCode();

        var remover = new HttpRequestMessage(HttpMethod.Delete, U("/api/notificacoes/dispositivos")) { Content = JsonContent.Create(new { token = TokenDoCelular }) };
        (await client.SendAsync(remover, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync(U("/api/notificacoes/teste"), null, Ct)).EnsureSuccessStatusCode();

        _push.Enviadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Push_AparelhoQueNaoExisteMais_ESaiDaLista()
    {
        var client = await Entrar();
        (await Registrar(client, TokenDoCelular)).EnsureSuccessStatusCode();
        _push.Invalidos.Add(TokenDoCelular);

        (await client.PostAsync(U("/api/notificacoes/teste"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/notificacoes/teste"), null, Ct)).EnsureSuccessStatusCode();

        _push.Enviadas.Count.ShouldBe(1, "o segundo envio já não acha o aparelho");
    }
}
