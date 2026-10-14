using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Cadastros;

/// <summary>Importação por planilha: modelo, validação linha a linha (simulação), confirmação e reimportação segura.</summary>
public sealed class ImportacaoTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private KamusApiFactory _factory = new(ambiente: "Development", configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record LinhaDto(int Numero, string Acao, string Descricao, List<string> Erros);
    private sealed record ValidacaoDto(Guid Id, int Total, int Validas, int ComErro, int Criar, int Atualizar, List<LinhaDto> Linhas);
    private sealed record ResultadoDto(int Criados, int Atualizados, int Ignorados);
    private sealed record PessoaDto(Guid Id, string Nome, string? Documento, string? Email, bool EhCliente, bool EhFornecedor, EnderecoDto Endereco, string Tipo);
    private sealed record EnderecoDto(string? Cidade);
    private sealed record PaginaDto(List<PessoaDto> Itens, int Total);
    private sealed record SkuDto(Guid CorId, string Tamanho, string CodigoDeBarras, decimal Preco, bool Ativo);
    private sealed record CorDto(Guid Id, string Nome);
    private sealed record ProdutoDto(Guid Id, string Referencia, string Descricao, List<CorDto> Cores, List<SkuDto> Skus);
    private sealed record ResumoDto(Guid Id, string Referencia);
    private sealed record PaginaProdutosDto(List<ResumoDto> Itens, int Total);

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

    private static byte[] Planilha(string[] cabecalho, params object?[][] linhas)
    {
        using var livro = new XLWorkbook();
        var aba = livro.Worksheets.Add("Dados");
        for (var c = 0; c < cabecalho.Length; c++) aba.Cell(1, c + 1).Value = cabecalho[c];
        for (var l = 0; l < linhas.Length; l++)
            for (var c = 0; c < linhas[l].Length; c++)
                aba.Cell(l + 2, c + 1).Value = linhas[l][c] switch { null => Blank.Value, decimal d => (double)d, var v => v.ToString() };
        using var ms = new MemoryStream();
        livro.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient client, string tipo, byte[] arquivo, string nome = "planilha.xlsx")
    {
        using var form = new MultipartFormDataContent();
        var conteudo = new ByteArrayContent(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(conteudo, "arquivo", nome);
        return await client.PostAsync(U($"/api/importacoes/{tipo}/validar"), form, Ct);
    }

    private static async Task<ValidacaoDto> Validar(HttpClient client, string tipo, byte[] arquivo)
    {
        var resposta = await Enviar(client, tipo, arquivo);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<ValidacaoDto>(Ct))!;
    }

    private static async Task<ResultadoDto> Confirmar(HttpClient client, Guid id)
    {
        var resposta = await client.PostAsync(U($"/api/importacoes/{id}/confirmar"), null, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<ResultadoDto>(Ct))!;
    }

    private static readonly string[] ColunasClientes = ["Nome", "CPF/CNPJ", "Telefone", "E-mail", "Cidade", "UF", "Aceita marketing"];

    [Fact]
    public async Task Modelo_ClientesEmXlsxComColunasEInstrucoes()
    {
        var client = await Entrar();

        var resposta = await client.GetAsync(U("/api/importacoes/clientes/modelo.xlsx"), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var livro = new XLWorkbook(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync(Ct)));
        var cabecalho = livro.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        cabecalho.ShouldContain("Nome");
        cabecalho.ShouldContain("CPF/CNPJ");
        cabecalho.ShouldContain("Calça");
        livro.Worksheet(2).Name.ShouldBe("Instruções");
    }

    [Fact]
    public async Task ValidarClientes_ClassificaCadaLinhaSemGravarNada()
    {
        var client = await Entrar();
        (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "Ana Antiga", documento = "111.444.777-35", ehCliente = true, email = "ana@antiga.com" }, Ct))
            .EnsureSuccessStatusCode();

        var validacao = await Validar(client, "clientes", Planilha(ColunasClientes,
            ["Maria Silva", "529.982.247-25", "(13) 99876-5432", "maria@email.com", "Santos", "SP", "Sim"],
            ["CPF Errado", "123.456.789-00", null, null, null, null, null],
            ["Maria Duplicada", "52998224725", null, null, null, null, null],
            ["Ana Atualizada", "111.444.777-35", null, null, "Guarujá", "SP", null]));

        validacao.Total.ShouldBe(4);
        validacao.Validas.ShouldBe(2);
        validacao.ComErro.ShouldBe(2);
        validacao.Criar.ShouldBe(1);
        validacao.Atualizar.ShouldBe(1);
        validacao.Linhas.Single(l => l.Numero == 3).Erros.ShouldContain("CPF inválido.");
        validacao.Linhas.Single(l => l.Numero == 4).Erros.ShouldContain(e => e.Contains("repetido", StringComparison.Ordinal));
        validacao.Linhas.Single(l => l.Numero == 5).Acao.ShouldBe("Atualizar");
        (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Total.ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmarClientes_ImportaAsValidasEReimportarNaoDuplica()
    {
        var client = await Entrar();
        (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "Ana Antiga", documento = "111.444.777-35", ehCliente = true, email = "ana@antiga.com" }, Ct))
            .EnsureSuccessStatusCode();
        var arquivo = Planilha(ColunasClientes,
            ["Maria Silva", "529.982.247-25", "(13) 99876-5432", "maria@email.com", "Santos", "SP", "Sim"],
            ["CPF Errado", "123.456.789-00", null, null, null, null, null],
            ["Ana Atualizada", "111.444.777-35", null, null, "Guarujá", "SP", null]);

        var resultado = await Confirmar(client, (await Validar(client, "clientes", arquivo)).Id);

        resultado.ShouldBe(new ResultadoDto(1, 1, 1));
        var clientes = (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Itens;
        clientes.Count.ShouldBe(2);
        var ana = clientes.Single(p => p.Documento == "111.444.777-35");
        ana.Nome.ShouldBe("Ana Atualizada");
        ana.Endereco.Cidade.ShouldBe("Guarujá");
        ana.Email.ShouldBe("ana@antiga.com"); // célula vazia não apaga

        var denovo = await Validar(client, "clientes", arquivo);
        denovo.Criar.ShouldBe(0);
        denovo.Atualizar.ShouldBe(2);
        await Confirmar(client, denovo.Id);
        (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Total.ShouldBe(2);
    }

    [Fact]
    public async Task Fornecedores_TipoPeloDocumentoEClienteExistenteGanhaOPapel()
    {
        var client = await Entrar();
        (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Juridica", nome = "Malharia Sul LTDA", documento = "11.222.333/0001-81", ehCliente = true }, Ct))
            .EnsureSuccessStatusCode();

        var validacao = await Validar(client, "fornecedores", Planilha(["Razão social", "Nome fantasia", "CPF/CNPJ"],
            ["Malharia Sul LTDA", "Malharia Sul", "11.222.333/0001-81"],
            ["Tecelagem Aurora LTDA", "Aurora", "11.444.777/0001-61"]));
        await Confirmar(client, validacao.Id);

        var fornecedores = (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=fornecedor"), Ct))!.Itens;
        fornecedores.Count.ShouldBe(2);
        fornecedores.ShouldAllBe(f => f.Tipo == "Juridica");
        fornecedores.Single(f => f.Documento == "11.222.333/0001-81").EhCliente.ShouldBeTrue();
    }

    private static readonly string[] ColunasProdutos = ["Referência", "Descrição", "Grade", "Cor", "Tamanho", "Código de barras", "Preço de venda", "Custo", "Preço do SKU"];

    [Fact]
    public async Task Produtos_UmaLinhaPorSkuMontaAGradeECriaCoresNovas()
    {
        var client = await Entrar();
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();

        var validacao = await Validar(client, "produtos", Planilha(ColunasProdutos,
            ["CAM-001", "Camiseta Gola V", "PP ao GG", "Azul", "P", null, 59.9m, 24m, null],
            ["CAM-001", null, "PP ao GG", "Azul", "M", null, null, null, null],
            ["CAM-001", null, "PP ao GG", "Preto", "P", "7891000315507", null, null, 64.9m],
            ["CAM-001", null, "PP ao GG", "Terracota", "M", null, null, null, null],
            ["CAM-001", null, "PP ao GG", "Azul", "XG", null, null, null, null],
            ["CAL-001", "Calça", "Grade Inexistente", "Azul", "40", null, 159.9m, null, null]));

        validacao.Validas.ShouldBe(4);
        validacao.ComErro.ShouldBe(2);
        validacao.Linhas.Single(l => l.Numero == 6).Erros.ShouldContain(e => e.Contains("XG", StringComparison.Ordinal));
        validacao.Linhas.Single(l => l.Numero == 7).Erros.ShouldContain(e => e.Contains("Grade Inexistente", StringComparison.Ordinal));
        validacao.Linhas.Single(l => l.Numero == 5).Descricao.ShouldContain("cor nova");

        var resultado = await Confirmar(client, validacao.Id);
        resultado.Criados.ShouldBe(1);

        var resumo = (await client.GetFromJsonAsync<PaginaProdutosDto>(U("/api/produtos?busca=CAM-001"), Ct))!.Itens.ShouldHaveSingleItem();
        var camiseta = (await client.GetFromJsonAsync<ProdutoDto>(U($"/api/produtos/{resumo.Id}"), Ct))!;
        camiseta.Cores.Select(c => c.Nome).ShouldBe(["Azul", "Preto", "Terracota"], ignoreOrder: true);
        var ativos = camiseta.Skus.Where(s => s.Ativo).ToList();
        ativos.Count.ShouldBe(4);
        var pretoP = ativos.Single(s => s.CodigoDeBarras == "7891000315507");
        pretoP.Tamanho.ShouldBe("P");
        pretoP.Preco.ShouldBe(64.9m);
    }

    [Fact]
    public async Task Produtos_ReimportarAtualizaEAtivaNovasCombinacoes()
    {
        var client = await Entrar();
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        await Confirmar(client, (await Validar(client, "produtos", Planilha(ColunasProdutos,
            ["CAM-001", "Camiseta Gola V", "PP ao GG", "Azul", "P", null, 59.9m, 24m, null]))).Id);

        var segunda = await Validar(client, "produtos", Planilha(ColunasProdutos,
            ["cam-001", "Camiseta Gola V Nova", "PP ao GG", "Azul", "G", null, 64.9m, null, null]));
        segunda.Atualizar.ShouldBe(1);
        (await Confirmar(client, segunda.Id)).Atualizados.ShouldBe(1);

        var resumo = (await client.GetFromJsonAsync<PaginaProdutosDto>(U("/api/produtos?busca=CAM-001"), Ct))!.Itens.ShouldHaveSingleItem();
        var camiseta = (await client.GetFromJsonAsync<ProdutoDto>(U($"/api/produtos/{resumo.Id}"), Ct))!;
        camiseta.Descricao.ShouldBe("Camiseta Gola V Nova");
        camiseta.Skus.Where(s => s.Ativo).Select(s => s.Tamanho).ShouldBe(["P", "G"], ignoreOrder: true);
    }

    [Fact]
    public async Task Permissoes_VendedorNaoImporta_ConfirmarDeOutroUsuario404()
    {
        var admin = await Entrar();
        var vendedor = await Entrar("vendedor@kamus.local");
        var validacao = await Validar(admin, "clientes", Planilha(ColunasClientes, ["Maria", null, null, null, null, null, null]));

        (await Enviar(vendedor, "clientes", Planilha(ColunasClientes, ["X", null, null, null, null, null, null]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.PostAsync(U($"/api/importacoes/{validacao.Id}/confirmar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArquivoInvalido_400()
    {
        var client = await Entrar();

        var resposta = await Enviar(client, "clientes", [1, 2, 3], "planilha.pdf");

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!.ShouldContain(".xlsx");
    }

    private sealed record NotificacaoDto(string Tipo, string Titulo);
    private sealed record NotificacoesDto(List<NotificacaoDto> Itens);

    [Fact]
    public async Task Grande_ProcessaEmSegundoPlanoEAvisaNoSininho()
    {
        await _factory.DisposeAsync();
        _factory = new KamusApiFactory(ambiente: "Development", configuracoes: new Dictionary<string, string>
        {
            ["Seed:SenhaAdmin"] = Senha,
            ["Importacao:LimiteSincrono"] = "1",
        });
        var client = await Entrar();
        var validacao = await Validar(client, "clientes", Planilha(ColunasClientes,
            ["Maria", null, null, null, null, null, null], ["Joana", null, null, null, null, null, null]));

        var resposta = await client.PostAsync(U($"/api/importacoes/{validacao.Id}/confirmar"), null, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        NotificacaoDto? aviso = null;
        for (var i = 0; i < 50 && aviso is null; i++)
        {
            await Task.Delay(100, Ct);
            aviso = (await client.GetFromJsonAsync<NotificacoesDto>(U("/api/notificacoes"), Ct))!.Itens.FirstOrDefault(n => n.Tipo == "importacao_concluida");
        }
        aviso.ShouldNotBeNull().Titulo.ShouldContain("2");
        (await client.GetFromJsonAsync<PaginaDto>(U("/api/pessoas?papel=cliente"), Ct))!.Total.ShouldBe(2);
    }
}
