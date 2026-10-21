using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Identidade;

/// <summary>Configurações → Empresas e Usuários (perfil Dono), senha provisória e limites da licença.</summary>
public sealed class GestaoDeEmpresasEUsuariosTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private const string NovoCnpj = "11.222.333/0001-81";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj, string Perfil);
    private sealed record UsuarioDaSessaoDto(Guid Id, string Nome, string Email, bool DeveTrocarSenha);
    private sealed record SessaoDto(string AccessToken, UsuarioDaSessaoDto Usuario, List<EmpresaDto> Empresas);
    private sealed record EmpresaDoTenantDto(Guid Id, string RazaoSocial, string NomeFantasia, string Cnpj);
    private sealed record AcessoDto(Guid EmpresaId, string Empresa, string Perfil);
    private sealed record UsuarioDto(Guid Id, string Nome, string Email, bool Ativo, bool DeveTrocarSenha, List<AcessoDto> Acessos);
    private sealed record CriadoDto(Guid Id, string SenhaProvisoria);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpResponseMessage> Login(string email, string senha)
    {
        using var client = _factory.CreateClient();
        return await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha }, Ct);
    }

    private async Task<(HttpClient Client, SessaoDto Sessao)> Entrar(string email = "admin@kamus.local", string senha = Senha)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        if (sessao.Empresas.Count > 0) client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return (client, sessao);
    }

    private static async Task<string?> Codigo(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).TryGetProperty("codigo", out var c) ? c.GetString() : null;

    private async Task Limites(int? cnpjs = null, int? usuarios = null)
    {
        await using var escopo = _factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
        (await db.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct)).DefinirLimites(cnpjs, null, usuarios);
        await db.SaveChangesAsync(Ct);
        escopo.ServiceProvider.GetRequiredService<CacheDeLicencas>().Invalidar(TenantDemo);
    }

    private static async Task<List<UsuarioDto>> Usuarios(HttpClient client) =>
        (await client.GetFromJsonAsync<List<UsuarioDto>>(U("/api/usuarios"), Ct))!;

    private static object NovoUsuario(string email, params (Guid EmpresaId, string Perfil)[] acessos) =>
        new { nome = "Bia Souza", email, acessos = acessos.Select(a => new { empresaId = a.EmpresaId, perfil = a.Perfil }) };

    // ---------- Empresas ----------

    [Fact]
    public async Task Empresas_Dono_VeTodasDoCliente()
    {
        var (client, _) = await Entrar();

        var empresas = await client.GetFromJsonAsync<List<EmpresaDoTenantDto>>(U("/api/empresas"), Ct);

        empresas.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Empresas_Vendedor_403()
    {
        var (client, _) = await Entrar("vendedor@kamus.local");

        (await client.GetAsync(U("/api/empresas"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CriarEmpresa_DonoGanhaAcessoNaHora()
    {
        var (client, _) = await Entrar();

        var resposta = await client.PostAsJsonAsync(U("/api/empresas"), new { razaoSocial = "Kamus Moda Outlet LTDA", nomeFantasia = "Kamus Outlet", cnpj = NovoCnpj }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        var (_, sessao) = await Entrar();
        sessao.Empresas.ShouldContain(e => e.Cnpj == NovoCnpj && e.Perfil == "Dono");
    }

    [Fact]
    public async Task CriarEmpresa_CnpjRepetido409_Invalido400()
    {
        var (client, _) = await Entrar();

        (await client.PostAsJsonAsync(U("/api/empresas"), new { razaoSocial = "X LTDA", nomeFantasia = "X", cnpj = "12.345.678/0001-95" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync(U("/api/empresas"), new { razaoSocial = "X LTDA", nomeFantasia = "X", cnpj = "11.111.111/1111-11" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarEmpresa_AcimaDoLimiteDaLicenca_409()
    {
        var (client, _) = await Entrar();
        await Limites(cnpjs: 2);

        var resposta = await client.PostAsJsonAsync(U("/api/empresas"), new { razaoSocial = "X LTDA", nomeFantasia = "X", cnpj = NovoCnpj }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Codigo(resposta)).ShouldBe("LIMITE_DA_LICENCA");
    }

    [Fact]
    public async Task EditarEmpresa_MudaNomesMasNaoOCnpj()
    {
        var (client, sessao) = await Entrar();
        var id = sessao.Empresas[0].Id;

        (await client.PutAsJsonAsync(U($"/api/empresas/{id}"), new { razaoSocial = "Nova Razão LTDA", nomeFantasia = "Nova Fantasia" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var empresas = await client.GetFromJsonAsync<List<EmpresaDoTenantDto>>(U("/api/empresas"), Ct);
        empresas!.Single(e => e.Id == id).NomeFantasia.ShouldBe("Nova Fantasia");
    }

    // ---------- Usuários ----------

    [Fact]
    public async Task Usuarios_DonoVeTodosComAcessos_VendedorNao()
    {
        var (admin, _) = await Entrar();
        var (vendedor, _) = await Entrar("vendedor@kamus.local");

        var usuarios = await Usuarios(admin);

        usuarios.Count.ShouldBe(2);
        usuarios.Single(u => u.Email == "vendedor@kamus.local").Acessos.ShouldHaveSingleItem().Perfil.ShouldBe("Vendedor");
        (await vendedor.GetAsync(U("/api/usuarios"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CriarUsuario_SenhaProvisoriaObrigaATrocar()
    {
        var (admin, sessao) = await Entrar();
        var resposta = await admin.PostAsJsonAsync(U("/api/usuarios"), NovoUsuario("bia@kamus.local", (sessao.Empresas[0].Id, "Vendedor")), Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<CriadoDto>(Ct))!;

        var (bia, sessaoBia) = await Entrar("bia@kamus.local", criado.SenhaProvisoria);
        sessaoBia.Usuario.DeveTrocarSenha.ShouldBeTrue();
        var bloqueada = await bia.GetAsync(U("/api/notificacoes"), Ct);
        bloqueada.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Codigo(bloqueada)).ShouldBe("TROCAR_SENHA");

        (await bia.PostAsJsonAsync(U("/api/auth/trocar-senha"), new { senhaAtual = criado.SenhaProvisoria, novaSenha = "MinhaSenha2026" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, depois) = await Entrar("bia@kamus.local", "MinhaSenha2026");
        depois.Usuario.DeveTrocarSenha.ShouldBeFalse();
        (await Login("bia@kamus.local", criado.SenhaProvisoria)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CriarUsuario_ComSenhaDefinidaPeloDono_EntraComElaETrocaNoPrimeiroAcesso()
    {
        var (admin, sessao) = await Entrar();
        var acessos = new[] { new { empresaId = sessao.Empresas[0].Id, perfil = "Vendedor" } };

        var fraca = await admin.PostAsJsonAsync(U("/api/usuarios"), new { nome = "Bia", email = "bia@kamus.local", senha = "123", acessos }, Ct);
        fraca.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resposta = await admin.PostAsJsonAsync(U("/api/usuarios"), new { nome = "Bia", email = "bia@kamus.local", senha = "Loja2026bia", acessos }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await resposta.Content.ReadFromJsonAsync<CriadoDto>(Ct))!.SenhaProvisoria.ShouldBe("Loja2026bia");

        var (_, sessaoBia) = await Entrar("bia@kamus.local", "Loja2026bia");
        sessaoBia.Usuario.DeveTrocarSenha.ShouldBeTrue();
    }

    [Fact]
    public async Task TrocarSenha_SenhaAtualErradaOuFraca_400()
    {
        var (admin, _) = await Entrar();

        (await admin.PostAsJsonAsync(U("/api/auth/trocar-senha"), new { senhaAtual = "errada", novaSenha = "MinhaSenha2026" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync(U("/api/auth/trocar-senha"), new { senhaAtual = Senha, novaSenha = "fraca" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarUsuario_EmailRepetido409_EmpresaDeOutroCliente400()
    {
        var (admin, sessao) = await Entrar();

        (await admin.PostAsJsonAsync(U("/api/usuarios"), NovoUsuario("vendedor@kamus.local", (sessao.Empresas[0].Id, "Vendedor")), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync(U("/api/usuarios"), NovoUsuario("bia@kamus.local", (Guid.NewGuid(), "Vendedor")), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync(U("/api/usuarios"), NovoUsuario("bia@kamus.local"), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarUsuario_AcimaDoLimiteDaLicenca_409()
    {
        var (admin, sessao) = await Entrar();
        await Limites(usuarios: 2);

        var resposta = await admin.PostAsJsonAsync(U("/api/usuarios"), NovoUsuario("bia@kamus.local", (sessao.Empresas[0].Id, "Vendedor")), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Codigo(resposta)).ShouldBe("LIMITE_DA_LICENCA");
    }

    [Fact]
    public async Task EditarUsuario_MudaNomeEPerfisPorEmpresa()
    {
        var (admin, sessao) = await Entrar();
        var vendedor = (await Usuarios(admin)).Single(u => u.Email == "vendedor@kamus.local");

        var resposta = await admin.PutAsJsonAsync(U($"/api/usuarios/{vendedor.Id}"), new
        {
            nome = "Vendedora Promovida",
            acessos = sessao.Empresas.Select(e => new { empresaId = e.Id, perfil = "Gerente" }),
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var (_, sessaoVendedor) = await Entrar("vendedor@kamus.local");
        sessaoVendedor.Usuario.Nome.ShouldBe("Vendedora Promovida");
        sessaoVendedor.Empresas.Count.ShouldBe(2);
        sessaoVendedor.Empresas.ShouldAllBe(e => e.Perfil == "Gerente");
    }

    [Fact]
    public async Task EditarUsuario_TirarOUltimoDono_400()
    {
        var (admin, sessao) = await Entrar();

        var resposta = await admin.PutAsJsonAsync(U($"/api/usuarios/{sessao.Usuario.Id}"), new
        {
            nome = "Admin",
            acessos = new[] { new { empresaId = sessao.Empresas[0].Id, perfil = "Gerente" } },
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Desativar_NaoEntraMais_ReativarVolta()
    {
        var (admin, _) = await Entrar();
        var vendedor = (await Usuarios(admin)).Single(u => u.Email == "vendedor@kamus.local");

        (await admin.PostAsync(U($"/api/usuarios/{vendedor.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Login("vendedor@kamus.local", Senha)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await admin.PostAsync(U($"/api/usuarios/{vendedor.Id}/reativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Login("vendedor@kamus.local", Senha)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Desativar_ASiMesmo_400()
    {
        var (admin, sessao) = await Entrar();

        (await admin.PostAsync(U($"/api/usuarios/{sessao.Usuario.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RedefinirSenha_GeraProvisoriaEDerrubaASenhaAntiga()
    {
        var (admin, _) = await Entrar();
        var vendedor = (await Usuarios(admin)).Single(u => u.Email == "vendedor@kamus.local");

        var resposta = await admin.PostAsync(U($"/api/usuarios/{vendedor.Id}/redefinir-senha"), null, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var nova = (await resposta.Content.ReadFromJsonAsync<CriadoDto>(Ct))!.SenhaProvisoria;
        (await Login("vendedor@kamus.local", Senha)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var (_, sessao) = await Entrar("vendedor@kamus.local", nova);
        sessao.Usuario.DeveTrocarSenha.ShouldBeTrue();
    }
}
