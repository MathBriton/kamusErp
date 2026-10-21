using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Cadastros;

/// <summary>Clientes e fornecedores (Pessoa com papéis), permissões por perfil, LGPD, aniversariantes, busca e relatório.</summary>
public sealed class PessoasTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).DateTime);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record PessoaDto(Guid Id, string Nome, string? Documento, string? Telefone, string? WhatsApp, bool EhCliente, bool EhFornecedor,
        bool Ativo, bool AceitaMarketing, DateTimeOffset? ConsentimentoMarketingEm, EnderecoDto Endereco, PreferenciasDto Preferencias);
    private sealed record EnderecoDto(string? Cidade, string? Uf);
    private sealed record PreferenciasDto(string? Calca);
    private sealed record PaginaDto(List<PessoaDto> Itens, int Total);
    private sealed record AniversarianteDto(Guid Id, string Nome, DateOnly DataNascimento, int Idade);
    private sealed record BuscaDto(List<GrupoDto> Grupos)
    {
        public IEnumerable<(string Grupo, ItemDto Item)> Itens => Grupos.SelectMany(g => g.Itens.Select(i => (g.Grupo, i)));
    }
    private sealed record GrupoDto(string Grupo, List<ItemDto> Itens);
    private sealed record ItemDto(string Titulo, string? Link);
    private sealed record AlteracaoDto(string Campo, string? Antes, string? Depois);
    private sealed record RegistroDto(string? Entidade, List<AlteracaoDto> Alteracoes);
    private sealed record HistoricoDto(List<RegistroDto> Itens);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return client;
    }

    private static object Cliente(string nome = "Maria Silva", string? cpf = "529.982.247-25", DateOnly? nascimento = null, bool marketing = true) => new
    {
        tipo = "Fisica",
        nome,
        documento = cpf,
        ehCliente = true,
        ehFornecedor = false,
        telefone = "(11) 98765-4321",
        email = "maria@email.com",
        dataNascimento = nascimento,
        aceitaMarketing = marketing,
        endereco = new { cidade = "Santos", uf = "sp" },
        preferencias = new { blusa = "M", calca = "40", calcado = "37" },
    };

    private static object Fornecedor() => new
    {
        tipo = "Juridica",
        nome = "Malharia Sul LTDA",
        nomeFantasia = "Malharia Sul",
        documento = "11.222.333/0001-81",
        ehCliente = false,
        ehFornecedor = true,
        telefone = "(47) 3333-4444",
    };

    private static async Task<PessoaDto> Criar(HttpClient client, object dados)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/pessoas"), dados, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<PessoaDto>(Ct))!;
    }

    [Fact]
    public async Task CriarCliente_FichaCompletaComConsentimento()
    {
        var client = await Entrar();

        var maria = await Criar(client, Cliente());

        maria.Documento.ShouldBe("529.982.247-25");
        maria.WhatsApp.ShouldBe("5511987654321");
        maria.Endereco.Uf.ShouldBe("SP");
        maria.Preferencias.Calca.ShouldBe("40");
        maria.AceitaMarketing.ShouldBeTrue();
        maria.ConsentimentoMarketingEm.ShouldNotBeNull();
        (await client.GetFromJsonAsync<PessoaDto>(U($"/api/pessoas/{maria.Id}"), Ct))!.Nome.ShouldBe("Maria Silva");
    }

    [Fact]
    public async Task Criar_DadosInvalidos_400ComTodosOsMotivos()
    {
        var client = await Entrar();

        var resposta = await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "", documento = "123", ehCliente = false }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detalhe = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!;
        detalhe.ShouldContain("nome");
        detalhe.ShouldContain("CPF inválido");
    }

    [Fact]
    public async Task Criar_DocumentoRepetido_409ComOCadastroExistente()
    {
        var client = await Entrar();
        var maria = await Criar(client, Cliente());

        var resposta = await client.PostAsJsonAsync(U("/api/pessoas"), Cliente("Outra Maria"), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("existenteId").GetGuid().ShouldBe(maria.Id);
    }

    [Fact]
    public async Task Listar_PorPapelEBuscaSemAcento()
    {
        var client = await Entrar();
        await Criar(client, Cliente("Mária José"));
        await Criar(client, Fornecedor());

        var clientes = (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!;
        var busca = (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente&busca=maria%20jose"), Ct))!;
        var porTelefone = (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?busca=3333-4444"), Ct))!;

        clientes.Itens.ShouldHaveSingleItem().Nome.ShouldBe("Mária José");
        busca.Total.ShouldBe(1);
        porTelefone.Itens.ShouldHaveSingleItem().Nome.ShouldBe("Malharia Sul LTDA");
    }

    [Fact]
    public async Task Editar_RegistraHistoricoComOsCamposDoEndereco()
    {
        var client = await Entrar();
        var maria = await Criar(client, Cliente());

        var resposta = await client.PutAsJsonAsync(U($"/api/pessoas/{maria.Id}"), new
        {
            tipo = "Fisica",
            nome = "Maria Silva",
            documento = "529.982.247-25",
            ehCliente = true,
            telefone = "11987654321",
            endereco = new { cidade = "Guarujá", uf = "SP" },
            aceitaMarketing = false,
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<PessoaDto>(Ct))!.ConsentimentoMarketingEm.ShouldBeNull();
        var historico = (await client.GetFromJsonAsync<HistoricoDto>(U("/api/auditoria?pagina=1&tamanho=50&entidade=Pessoa"), Ct))!;
        historico.Itens.SelectMany(r => r.Alteracoes).ShouldContain(a => a.Campo == "EnderecoCidade" && a.Antes == "Santos" && a.Depois == "Guarujá");
    }

    [Fact]
    public async Task Vendedor_CadastraClienteMasNaoVeFornecedores()
    {
        var admin = await Entrar();
        await Criar(admin, Fornecedor());
        var vendedor = await Entrar("vendedor@kamus.local");

        await Criar(vendedor, Cliente());
        (await vendedor.GetAsync(U("/api/pessoas?papel=fornecedor"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.PostAsJsonAsync(U("/api/pessoas"), Fornecedor(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var todos = (await vendedor.GetFromJsonAsync<PaginaDto>(U("/api/pessoas"), Ct))!;
        todos.Itens.ShouldAllBe(p => p.EhCliente);
    }

    [Fact]
    public async Task InativarEReativar_SoGestao()
    {
        var admin = await Entrar();
        var vendedor = await Entrar("vendedor@kamus.local");
        var maria = await Criar(admin, Cliente());

        (await vendedor.PostAsync(U($"/api/pessoas/{maria.Id}/inativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync(U($"/api/pessoas/{maria.Id}/inativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Total.ShouldBe(0);
        (await admin.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente&situacao=inativos"), Ct))!.Total.ShouldBe(1);
        (await admin.PostAsync(U($"/api/pessoas/{maria.Id}/reativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Anonimizar_SoDono_ApagaDadosPessoais()
    {
        var admin = await Entrar();
        var vendedor = await Entrar("vendedor@kamus.local");
        var maria = await Criar(admin, Cliente());

        (await vendedor.PostAsync(U($"/api/pessoas/{maria.Id}/anonimizar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync(U($"/api/pessoas/{maria.Id}/anonimizar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var depois = (await admin.GetFromJsonAsync<PessoaDto>(U($"/api/pessoas/{maria.Id}"), Ct))!;
        depois.Nome.ShouldBe("Cliente anonimizado");
        depois.Documento.ShouldBeNull();
        depois.Telefone.ShouldBeNull();
        (await admin.PutAsJsonAsync(U($"/api/pessoas/{maria.Id}"), Cliente(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Aniversariantes_DaSemanaComIdade()
    {
        var client = await Entrar();
        var hoje = Hoje;
        await Criar(client, Cliente("Aniversariante Hoje", cpf: null, nascimento: new DateOnly(1990, hoje.Month, Math.Min(hoje.Day, 28))));
        await Criar(client, Cliente("Mês Que Vem", cpf: null, nascimento: hoje.AddMonths(2).AddYears(-30)));

        var semana = await client.GetFromJsonAsync<List<AniversarianteDto>>(U("/api/pessoas/aniversariantes?periodo=semana"), Ct);

        var aniversariante = semana.ShouldNotBeNull().ShouldHaveSingleItem();
        aniversariante.Nome.ShouldBe("Aniversariante Hoje");
    }

    [Fact]
    public async Task PesquisaGlobal_EncontraClientePorNomeEDocumento()
    {
        var client = await Entrar();
        await Criar(client, Cliente());

        var porNome = (await client.GetFromJsonAsync<BuscaDto>(U("/api/busca?q=maria"), Ct))!;
        var porCpf = (await client.GetFromJsonAsync<BuscaDto>(U("/api/busca?q=52998224725"), Ct))!;

        porNome.Itens.ShouldContain(r => r.Grupo == "Clientes" && r.Item.Titulo == "Maria Silva" && r.Item.Link!.StartsWith("/cadastros/clientes", StringComparison.Ordinal));
        porCpf.Itens.ShouldContain(r => r.Item.Titulo == "Maria Silva");
    }

    [Fact]
    public async Task Relatorio_ClientesEmCsv()
    {
        var client = await Entrar();
        await Criar(client, Cliente());

        var csv = await client.GetStringAsync(U("/api/relatorios/clientes.csv"), Ct);

        csv.ShouldContain("Maria Silva");
        csv.ShouldContain("529.982.247-25");
    }

    [Fact]
    public async Task Listar_PaginaComTotal()
    {
        var client = await Entrar();
        await Criar(client, Cliente("Ana Costa", cpf: "111.444.777-35"));
        await Criar(client, Cliente());

        var pagina = (await client.GetFromJsonAsync<JsonElement>(U("/api/pessoas?papel=cliente&pagina=2&tamanho=1"), Ct))!;

        pagina.GetProperty("total").GetInt32().ShouldBe(2);
        pagina.GetProperty("pagina").GetInt32().ShouldBe(2);
        pagina.GetProperty("tamanho").GetInt32().ShouldBe(1);
        pagina.GetProperty("itens").EnumerateArray().Single().GetProperty("nome").GetString().ShouldBe("Maria Silva");
    }

    [Fact]
    public async Task Relatorio_SoOsSelecionados()
    {
        var client = await Entrar();
        var ana = await Criar(client, Cliente("Ana Costa", cpf: "111.444.777-35"));
        await Criar(client, Cliente());

        var csv = await client.GetStringAsync(U($"/api/relatorios/clientes.csv?ids={ana.Id}&busca=maria"), Ct);

        csv.ShouldContain("Ana Costa");
        csv.ShouldNotContain("Maria Silva");
        var dados = await client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/clientes/dados?ids={ana.Id},{Guid.NewGuid()}"), Ct);
        dados.GetProperty("filtros")[0].GetProperty("valor").GetString().ShouldBe("1 cliente");
    }

    [Fact]
    public async Task SituacaoEmLote_InativaEReativaVarios_VendedorNao()
    {
        var admin = await Entrar();
        var ana = await Criar(admin, Cliente("Ana Costa", cpf: "111.444.777-35"));
        var maria = await Criar(admin, Cliente());

        var inativar = await admin.PostAsJsonAsync(U("/api/pessoas/situacao"), new { ids = new[] { ana.Id, maria.Id }, ativo = false }, Ct);
        inativar.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await inativar.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("alterados").GetInt32().ShouldBe(2);
        (await admin.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente&situacao=inativos"), Ct))!.Total.ShouldBe(2);

        (await admin.PostAsJsonAsync(U("/api/pessoas/situacao"), new { ids = new[] { ana.Id }, ativo = true }, Ct)).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Itens.Single().Id.ShouldBe(ana.Id);
        (await admin.PostAsJsonAsync(U("/api/pessoas/situacao"), new { ids = Array.Empty<Guid>(), ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var vendedor = await Entrar("vendedor@kamus.local");
        (await vendedor.PostAsJsonAsync(U("/api/pessoas/situacao"), new { ids = new[] { ana.Id }, ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
