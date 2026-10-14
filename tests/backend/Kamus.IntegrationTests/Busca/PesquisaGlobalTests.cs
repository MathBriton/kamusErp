using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Busca;

public sealed class PesquisaGlobalTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record ItemDto(string Tipo, string Id, string Titulo, string? Subtitulo, string? Link);
    private sealed record GrupoDto(string Grupo, List<ItemDto> Itens);
    private sealed record RespostaDto(string Termo, List<GrupoDto> Grupos);

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

    private static async Task<RespostaDto> Buscar(HttpClient client, string termo) =>
        (await client.GetFromJsonAsync<RespostaDto>(new Uri($"/api/busca?q={Uri.EscapeDataString(termo)}", UriKind.Relative), Ct))!;

    private static List<ItemDto> Itens(RespostaDto r, string grupo) => r.Grupos.SingleOrDefault(g => g.Grupo == grupo)?.Itens ?? [];

    [Fact]
    public async Task EmpresaPorParteDoNome_IgnorandoMaiusculas()
    {
        var client = await Entrar();

        var resposta = await Buscar(client, "SHOP");

        Itens(resposta, "Empresas").ShouldHaveSingleItem().Titulo.ShouldBe("Kamus Moda Shopping");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("12.345.678")]
    public async Task EmpresaPeloCnpj_ComOuSemMascara(string termo)
    {
        var client = await Entrar();

        var empresa = Itens(await Buscar(client, termo), "Empresas").ShouldHaveSingleItem();

        empresa.Titulo.ShouldBe("Kamus Moda Centro");
        empresa.Subtitulo.ShouldBe("12.345.678/0001-95");
        empresa.Tipo.ShouldBe("empresa");
    }

    [Fact]
    public async Task Vendedor_NaoEncontraEmpresaQueNaoAcessa()
    {
        var client = await Entrar("vendedor@kamus.local");

        Itens(await Buscar(client, "shopping"), "Empresas").ShouldBeEmpty();
    }

    [Fact]
    public async Task Dono_EncontraUsuariosPorNomeOuEmail()
    {
        var client = await Entrar();

        var usuario = Itens(await Buscar(client, "vendedora"), "Usuários").ShouldHaveSingleItem();

        usuario.Subtitulo.ShouldBe("vendedor@kamus.local");
    }

    [Fact]
    public async Task Vendedor_NaoPesquisaUsuarios()
    {
        var client = await Entrar("vendedor@kamus.local");

        Itens(await Buscar(client, "admin"), "Usuários").ShouldBeEmpty();
    }

    [Fact]
    public async Task TermoMuitoCurto_NaoPesquisa()
    {
        var client = await Entrar();

        (await Buscar(client, "k")).Grupos.ShouldBeEmpty();
    }

    [Fact]
    public async Task SemLogin_Retorna401()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync(new Uri("/api/busca?q=kamus", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
