using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Identidade;

/// <summary>Empresa (CNPJ) ativa enviada pelo frontend no header X-Empresa-Id e validada no backend.</summary>
public sealed class EmpresaAtivaTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private const string CnpjCentro = "12.345.678/0001-95";
    private const string CnpjShopping = "98.765.432/0001-98";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id, string NomeFantasia, string Cnpj, string Perfil);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record EmpresaAtivaDto(bool Consolidado, List<EmpresaDto> Empresas);
    private sealed record Problema(string? Detail);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<(HttpClient Client, SessaoDto Sessao)> Entrar(string email)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        return (client, sessao);
    }

    private static async Task<HttpResponseMessage> Ativa(HttpClient client, string? empresa, HttpMethod? metodo = null)
    {
        var request = new HttpRequestMessage(metodo ?? HttpMethod.Get, new Uri("/api/empresas/ativa", UriKind.Relative));
        if (empresa is not null) request.Headers.Add("X-Empresa-Id", empresa);
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task EmpresaQueOUsuarioAcessa_EhAceita()
    {
        var (client, sessao) = await Entrar("admin@kamus.local");
        var centro = sessao.Empresas.Single(e => e.Cnpj == CnpjCentro);

        var resposta = await Ativa(client, centro.Id.ToString());

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<EmpresaAtivaDto>(Ct);
        corpo!.Consolidado.ShouldBeFalse();
        corpo.Empresas.Single().Cnpj.ShouldBe(CnpjCentro);
    }

    [Fact]
    public async Task EmpresaSemAcesso_Retorna403()
    {
        var (admin, sessaoAdmin) = await Entrar("admin@kamus.local");
        var shopping = sessaoAdmin.Empresas.Single(e => e.Cnpj == CnpjShopping);
        var (vendedor, _) = await Entrar("vendedor@kamus.local");

        var resposta = await Ativa(vendedor, shopping.Id.ToString());

        resposta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadFromJsonAsync<Problema>(Ct))!.Detail.ShouldBe("Você não tem acesso a esta empresa.");
        admin.Dispose();
    }

    [Fact]
    public async Task EmpresaInexistente_Retorna403()
    {
        var (client, _) = await Entrar("admin@kamus.local");

        (await Ativa(client, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VisaoConsolidada_ParaDono_RetornaTodasAsEmpresas()
    {
        var (client, _) = await Entrar("admin@kamus.local");

        var resposta = await Ativa(client, "todas");

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<EmpresaAtivaDto>(Ct);
        corpo!.Consolidado.ShouldBeTrue();
        corpo.Empresas.Select(e => e.Cnpj).ShouldBe([CnpjCentro, CnpjShopping], ignoreOrder: true);
    }

    [Fact]
    public async Task VisaoConsolidada_ParaQuemNaoEhDono_Retorna403()
    {
        var (client, _) = await Entrar("vendedor@kamus.local");

        var resposta = await Ativa(client, "todas");

        resposta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadFromJsonAsync<Problema>(Ct))!.Detail.ShouldBe("A visão consolidada é exclusiva do perfil Dono.");
    }

    [Fact]
    public async Task VisaoConsolidada_EmOperacaoDeEscrita_Retorna400()
    {
        var (client, _) = await Entrar("admin@kamus.local");

        var resposta = await Ativa(client, "todas", HttpMethod.Post);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadFromJsonAsync<Problema>(Ct))!.Detail!.ShouldContain("Selecione uma empresa");
    }

    [Fact]
    public async Task HeaderInvalido_Retorna400()
    {
        var (client, _) = await Entrar("admin@kamus.local");

        (await Ativa(client, "nao-e-um-guid")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SemHeader_Retorna400()
    {
        var (client, _) = await Entrar("admin@kamus.local");

        (await Ativa(client, null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RotasDeAutenticacao_IgnoramOHeader()
    {
        var (client, _) = await Entrar("admin@kamus.local");
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/auth/logout", UriKind.Relative));
        request.Headers.Add("X-Empresa-Id", "todas");

        (await client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UsuariaVendedora_VeSomenteAEmpresaEmQueTrabalha()
    {
        var (_, sessao) = await Entrar("vendedor@kamus.local");

        sessao.Empresas.ShouldHaveSingleItem().Perfil.ShouldBe("Vendedor");
    }
}
