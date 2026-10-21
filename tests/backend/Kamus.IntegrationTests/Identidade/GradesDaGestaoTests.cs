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

/// <summary>Usuários e Empresas no padrão de grade: filtros, paginação, situação em lote e exportação.</summary>
public sealed class GradesDaGestaoTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record UsuarioDaSessaoDto(Guid Id, string Email);
    private sealed record SessaoDto(string AccessToken, UsuarioDaSessaoDto Usuario, List<EmpresaDto> Empresas);
    private sealed record AcessoDto(Guid EmpresaId, string Empresa, string Perfil);
    private sealed record UsuarioDto(Guid Id, string Nome, string Email, bool Ativo, List<AcessoDto> Acessos);
    private sealed record PaginaDeUsuariosDto(List<UsuarioDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record EmpresaDoTenantDto(Guid Id, string RazaoSocial, string NomeFantasia, string Cnpj);
    private sealed record PaginaDeEmpresasDto(List<EmpresaDoTenantDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record CriadoDto(Guid Id);
    private sealed record AlteradosDto(int Alterados);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<(HttpClient Client, SessaoDto Sessao)> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return (client, sessao);
    }

    private static async Task<Guid> CriarUsuario(HttpClient client, string nome, string email, Guid empresaId, string perfil)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/usuarios"), new { nome, email, acessos = new[] { new { empresaId, perfil } } }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<CriadoDto>(Ct))!.Id;
    }

    private async Task LimiteDeUsuarios(int? usuarios)
    {
        await using var escopo = _factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
        (await db.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct)).DefinirLimites(null, null, usuarios);
        await db.SaveChangesAsync(Ct);
        escopo.ServiceProvider.GetRequiredService<CacheDeLicencas>().Invalidar(TenantDemo);
    }

    private static async Task<string> Detalhe(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!;

    private static Guid Shopping(SessaoDto s) => s.Empresas.Single(e => e.NomeFantasia == "Kamus Moda Shopping").Id;

    [Fact]
    public async Task Usuarios_ComPaginaFiltraPorBuscaSituacaoEmpresaEPerfil_SemPaginaContinuaLista()
    {
        var (admin, sessao) = await Entrar();
        var bia = await CriarUsuario(admin, "Bia Souza", "bia@kamus.local", Shopping(sessao), "Gerente");
        (await admin.PostAsync(U($"/api/usuarios/{bia}/desativar"), null, Ct)).EnsureSuccessStatusCode();

        var lista = (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/usuarios"), Ct))!;
        var pagina = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U("/api/usuarios?pagina=1&tamanho=2"), Ct))!;
        var ativos = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U("/api/usuarios?situacao=ativos&pagina=1&tamanho=20"), Ct))!;
        var inativos = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U("/api/usuarios?situacao=inativos&pagina=1&tamanho=20"), Ct))!;
        var busca = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U("/api/usuarios?busca=vendedor&pagina=1&tamanho=20"), Ct))!;
        var doShopping = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U($"/api/usuarios?empresaId={Shopping(sessao)}&pagina=1&tamanho=20"), Ct))!;
        var gerentes = (await admin.GetFromJsonAsync<PaginaDeUsuariosDto>(U("/api/usuarios?perfil=Gerente&pagina=1&tamanho=20"), Ct))!;

        lista.Count.ShouldBe(3);
        (pagina.Total, pagina.Itens.Count, pagina.Tamanho).ShouldBe((3, 2, 2));
        ativos.Itens.Select(u => u.Email).ShouldBe(["admin@kamus.local", "vendedor@kamus.local"], ignoreOrder: true);
        inativos.Itens.Single().Email.ShouldBe("bia@kamus.local");
        busca.Itens.Single().Email.ShouldBe("vendedor@kamus.local");
        doShopping.Itens.Select(u => u.Email).ShouldBe(["admin@kamus.local", "bia@kamus.local"], ignoreOrder: true);
        gerentes.Itens.Single().Email.ShouldBe("bia@kamus.local");
    }

    [Fact]
    public async Task Usuarios_SituacaoEmLote_MesmasRegrasDaUnitaria()
    {
        var (admin, sessao) = await Entrar();
        var bia = await CriarUsuario(admin, "Bia Souza", "bia@kamus.local", Shopping(sessao), "Vendedor");
        var vendedora = (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/usuarios"), Ct))!.Single(u => u.Email == "vendedor@kamus.local").Id;

        var comigo = await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { bia, sessao.Usuario.Id }, ativo = false }, Ct);
        comigo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Detalhe(comigo)).ShouldContain("próprio usuário");
        (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/usuarios"), Ct))!.ShouldAllBe(u => u.Ativo); // tudo ou nada

        var desativar = await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { bia, vendedora }, ativo = false }, Ct);
        desativar.StatusCode.ShouldBe(HttpStatusCode.OK, await desativar.Content.ReadAsStringAsync(Ct));
        (await desativar.Content.ReadFromJsonAsync<AlteradosDto>(Ct))!.Alterados.ShouldBe(2);
        using (var anonimo = _factory.CreateClient())
            (await anonimo.PostAsJsonAsync(U("/api/auth/login"), new { email = "vendedor@kamus.local", senha = Senha }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await LimiteDeUsuarios(2);
        var acima = await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { bia, vendedora }, ativo = true }, Ct);
        acima.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await LimiteDeUsuarios(null);
        (await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { bia, vendedora }, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetFromJsonAsync<List<UsuarioDto>>(U("/api/usuarios"), Ct))!.ShouldAllBe(u => u.Ativo);

        (await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = Array.Empty<Guid>(), ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { Guid.NewGuid() }, ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var (vendedor, _) = await Entrar("vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/usuarios/situacao"), new { ids = new[] { bia }, ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Usuarios_RelatorioComFiltrosOuSoOsSelecionados_SemSenhas()
    {
        var (admin, sessao) = await Entrar();
        var bia = await CriarUsuario(admin, "Bia Souza", "bia@kamus.local", Shopping(sessao), "Gerente");

        var tudo = await admin.GetStringAsync(U("/api/relatorios/usuarios.csv"), Ct);
        var gerentes = await admin.GetStringAsync(U("/api/relatorios/usuarios.csv?perfil=Gerente"), Ct);
        var selecionada = await admin.GetStringAsync(U($"/api/relatorios/usuarios.csv?ids={bia}"), Ct);

        tudo.ShouldContain("admin@kamus.local");
        tudo.ShouldContain("Kamus Moda Shopping: Gerente");
        gerentes.ShouldContain("bia@kamus.local");
        gerentes.ShouldNotContain("vendedor@kamus.local");
        selecionada.ShouldContain("bia@kamus.local");
        selecionada.ShouldNotContain("admin@kamus.local");
        var (vendedor, _) = await Entrar("vendedor@kamus.local");
        (await vendedor.GetAsync(U("/api/relatorios/usuarios.csv"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Empresas_ComPaginaEBusca_SemPaginaContinuaLista_ERelatorio()
    {
        var (admin, sessao) = await Entrar();

        var lista = (await admin.GetFromJsonAsync<List<EmpresaDoTenantDto>>(U("/api/empresas"), Ct))!;
        var pagina = (await admin.GetFromJsonAsync<PaginaDeEmpresasDto>(U("/api/empresas?pagina=1&tamanho=1"), Ct))!;
        var busca = (await admin.GetFromJsonAsync<PaginaDeEmpresasDto>(U("/api/empresas?busca=shopping&pagina=1&tamanho=20"), Ct))!;
        var porCnpj = (await admin.GetFromJsonAsync<PaginaDeEmpresasDto>(U("/api/empresas?busca=12345678&pagina=1&tamanho=20"), Ct))!;

        lista.Count.ShouldBe(2);
        (pagina.Total, pagina.Itens.Count).ShouldBe((2, 1));
        busca.Itens.Single().NomeFantasia.ShouldBe("Kamus Moda Shopping");
        porCnpj.Itens.Single().NomeFantasia.ShouldBe("Kamus Moda Centro");

        var csv = await admin.GetStringAsync(U("/api/relatorios/empresas.csv"), Ct);
        csv.ShouldContain("98.765.432/0001-98");
        var selecionada = await admin.GetStringAsync(U($"/api/relatorios/empresas.csv?ids={Shopping(sessao)}"), Ct);
        selecionada.ShouldContain("Kamus Moda Shopping LTDA");
        selecionada.ShouldNotContain("Kamus Moda Centro LTDA");
    }
}
