using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Cadastros;

public sealed class VendedoresTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record VendedorDto(Guid Id, string Nome, decimal ComissaoPercentual, Guid? UsuarioId, string? UsuarioNome, bool Ativo);
    private sealed record UsuarioDto(Guid Id, string Nome, string Email);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return client;
    }

    [Fact]
    public async Task Gestao_CadastraVendedorVinculadoAoUsuario()
    {
        var admin = await Entrar();
        var usuarios = (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/vendedores/usuarios"), Ct))!;
        var vendedora = usuarios.Single(u => u.Email == "vendedor@kamus.local");

        var resposta = await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Vendedora Demo", comissaoPercentual = 3.5m, usuarioId = vendedora.Id }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        var lista = (await admin.GetFromJsonAsync<List<VendedorDto>>(U("/api/vendedores"), Ct))!;
        var v = lista.ShouldHaveSingleItem();
        v.ComissaoPercentual.ShouldBe(3.5m);
        v.UsuarioNome.ShouldBe("Vendedora Demo");
    }

    [Fact]
    public async Task UsuarioJaVinculado_409_UsuarioDeOutroCliente_400_ComissaoInvalida_400()
    {
        var admin = await Entrar();
        var usuarioId = (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/vendedores/usuarios"), Ct))!.First().Id;
        await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "A", comissaoPercentual = 3m, usuarioId }, Ct);

        (await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "B", comissaoPercentual = 3m, usuarioId }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "C", comissaoPercentual = 3m, usuarioId = Guid.NewGuid() }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "D", comissaoPercentual = 150m }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditarInativarReativar()
    {
        var admin = await Entrar();
        var criado = await (await admin.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Ana", comissaoPercentual = 3m }, Ct)).Content.ReadFromJsonAsync<VendedorDto>(Ct);

        (await admin.PutAsJsonAsync(U($"/api/vendedores/{criado!.Id}"), new { nome = "Ana Paula", comissaoPercentual = 4m }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostAsync(U($"/api/vendedores/{criado.Id}/inativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var lista = (await admin.GetFromJsonAsync<List<VendedorDto>>(U("/api/vendedores"), Ct))!;
        lista.Single().Nome.ShouldBe("Ana Paula");
        lista.Single().Ativo.ShouldBeFalse();
        (await admin.PostAsync(U($"/api/vendedores/{criado.Id}/reativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Vendedor_VeAListaMasNaoAltera()
    {
        var vendedor = await Entrar("vendedor@kamus.local");

        (await vendedor.GetAsync(U("/api/vendedores"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await vendedor.PostAsJsonAsync(U("/api/vendedores"), new { nome = "X", comissaoPercentual = 1m }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
