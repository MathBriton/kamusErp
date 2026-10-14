using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Kamus.IntegrationTests.Infra;
using UglyToad.PdfPig;

namespace Kamus.IntegrationTests.Relatorios;

/// <summary>Exportação de relatórios (em memória), usando o histórico de alterações como relatório real.</summary>
public sealed class ExportacaoTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private const string Rota = "/api/relatorios/historico-de-alteracoes";

    private KamusApiFactory _factory = CriarApi();

    private static KamusApiFactory CriarApi(string? limiteSincrono = null)
    {
        var config = new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha };
        if (limiteSincrono is not null) config["Relatorios:LimiteSincrono"] = limiteSincrono;
        return new KamusApiFactory(ambiente: "Development", configuracoes: config);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id, string Cnpj);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record NotificacaoDto(string Tipo, string Mensagem, string? Link);
    private sealed record ListaDto(List<NotificacaoDto> Itens);
    private sealed record RegistroDto(string? Evento, string Descricao);
    private sealed record PaginaDto(List<RegistroDto> Itens);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> Entrar(string email = "admin@kamus.local")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha = Senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.Cnpj == "12.345.678/0001-95").Id.ToString());
        return client;
    }

    private static Task<HttpResponseMessage> Exportar(HttpClient client, string formato, string filtros = "") =>
        client.GetAsync(new Uri($"{Rota}.{formato}{filtros}", UriKind.Relative), Ct);

    [Fact]
    public async Task Excel_BaixaArquivoComNomeTipoEConteudoDoHistorico()
    {
        var client = await Entrar();

        var resposta = await Exportar(client, "xlsx");

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var nome = resposta.Content.Headers.ContentDisposition!.FileNameStar ?? resposta.Content.Headers.ContentDisposition.FileName!;
        nome.ShouldStartWith("historico-de-alteracoes-");
        nome.ShouldEndWith(".xlsx");

        using var planilha = new XLWorkbook(await resposta.Content.ReadAsStreamAsync(Ct));
        var textos = planilha.Worksheets.First().CellsUsed().Select(c => c.GetString()).ToList();
        textos.ShouldContain(t => t.Contains("Kamus Moda Centro", StringComparison.Ordinal) && t.Contains("12.345.678/0001-95", StringComparison.Ordinal));
        textos.ShouldContain("Login de admin@kamus.local");
    }

    [Fact]
    public async Task Pdf_GeraDocumentoComTituloEEmpresa()
    {
        var client = await Entrar();

        var resposta = await Exportar(client, "pdf");

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(Ct);
        using var pdf = PdfDocument.Open(bytes);
        var texto = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        texto.ShouldContain("Histórico");
        texto.ShouldContain("Centro");
    }

    [Fact]
    public async Task Csv_RespeitaOsFiltrosDaTela()
    {
        var client = await Entrar();

        var resposta = await Exportar(client, "csv", "?tipo=Evento");

        var linhas = Encoding.UTF8.GetString(await resposta.Content.ReadAsByteArrayAsync(Ct)).TrimStart('﻿')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        linhas[0].ShouldStartWith("Data/hora;Tipo;Descrição;Usuário");
        linhas.Skip(1).ShouldNotBeEmpty();
        linhas.Skip(1).ShouldAllBe(l => l.Split(';', StringSplitOptions.None)[1] == "Evento");
    }

    [Fact]
    public async Task Exportacao_FicaRegistradaNoHistorico()
    {
        var client = await Entrar();
        await Exportar(client, "pdf");

        var pagina = await client.GetFromJsonAsync<PaginaDto>(new Uri("/api/auditoria?tipo=Evento&evento=exportacao", UriKind.Relative), Ct);

        pagina!.Itens.ShouldHaveSingleItem().Descricao.ShouldContain("Histórico de alterações");
    }

    [Fact]
    public async Task Vendedor_NaoExportaRelatorioRestrito()
    {
        var client = await Entrar("vendedor@kamus.local");

        (await Exportar(client, "xlsx")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/relatorios/nao-existe.pdf")]
    [InlineData("/api/relatorios/historico-de-alteracoes.docx")]
    public async Task RelatorioOuFormatoInexistente_Retorna404(string rota)
    {
        var client = await Entrar();

        (await client.GetAsync(new Uri(rota, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RelatorioGrande_GeraEmSegundoPlanoEAvisaNoSininhoComLinkParaBaixar()
    {
        await _factory.DisposeAsync();
        _factory = CriarApi(limiteSincrono: "1");
        var client = await Entrar();

        (await Exportar(client, "xlsx")).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        NotificacaoDto? pronto = null;
        for (var i = 0; i < 50 && pronto is null; i++)
        {
            await Task.Delay(200, Ct);
            var lista = await client.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct);
            pronto = lista!.Itens.FirstOrDefault(n => n.Tipo == "relatorio_pronto");
        }
        pronto.ShouldNotBeNull();
        pronto.Link.ShouldNotBeNull().ShouldStartWith("/api/relatorios/arquivos/");

        // O link da notificação abre direto no navegador (sem cabeçalho de autenticação)
        using var navegador = _factory.CreateClient();
        var arquivo = await navegador.GetAsync(new Uri(pronto.Link, UriKind.Relative), Ct);
        arquivo.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var planilha = new XLWorkbook(await arquivo.Content.ReadAsStreamAsync(Ct));
        planilha.Worksheets.First().CellsUsed().ShouldContain(c => c.GetString() == "Login de admin@kamus.local");
    }

    [Fact]
    public async Task ArquivoInexistenteOuExpirado_Retorna404()
    {
        using var navegador = _factory.CreateClient();

        (await navegador.GetAsync(new Uri($"/api/relatorios/arquivos/{Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
