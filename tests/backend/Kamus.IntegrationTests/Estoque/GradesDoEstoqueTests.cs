using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kamus.UnitTests.Estoque;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Telas do estoque no padrão de grade: filtros, paginação, ações em lote e exportação (inclusive só as linhas selecionadas).</summary>
public sealed class GradesDoEstoqueTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record LojaCompletaDto(Guid Id, string Nome, bool Ativa, int Quantidade);
    private sealed record PaginaDeLojasDto(List<LojaCompletaDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record ItemDaPosicaoDto(Guid ProdutoId, string Referencia, int Quantidade);
    private sealed record PosicaoDto(List<ItemDaPosicaoDto> Itens, int Total, int Pagina, int Tamanho, int TotalDePecas, decimal? ValorEmCusto, decimal ValorEmVenda);
    private sealed record ResumoDaNotaDto(Guid Id, string Numero, string Status);
    private sealed record PaginaDeNotasDto(List<ResumoDaNotaDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record InventarioDto(Guid Id, Guid LojaId, string Loja, string Status);
    private sealed record PaginaDeInventariosDto(List<InventarioDto> Itens, int Total, int Pagina, int Tamanho);
    private sealed record AlteradosDto(int Alterados);

    private static readonly ExemploDeNfe.Item Camisetas = new("AUR-100", "SEM GTIN", "CAMISETA BASICA SORTIDA", "UN", 10, 25m);

    private static async Task<LojaCompletaDto> CriarLoja(HttpClient client, string nome)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<LojaCompletaDto>(Ct))!;
    }

    private static async Task<string> Detalhe(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!;

    [Fact]
    public async Task Lojas_ComPaginaDevolvemPaginaComFiltros_SemPaginaContinuamLista()
    {
        var client = await Entrar(_api);
        await Lojas(client);
        var deposito = await CriarLoja(client, "Depósito");
        await CriarLoja(client, "Outlet");
        (await client.PostAsync(U($"/api/estoque/lojas/{deposito.Id}/desativar"), null, Ct)).EnsureSuccessStatusCode();

        var lista = (await client.GetFromJsonAsync<List<LojaCompletaDto>>(U("/api/estoque/lojas"), Ct))!;
        var pagina = (await client.GetFromJsonAsync<PaginaDeLojasDto>(U("/api/estoque/lojas?pagina=1&tamanho=2"), Ct))!;
        var inativas = (await client.GetFromJsonAsync<PaginaDeLojasDto>(U("/api/estoque/lojas?situacao=inativas&pagina=1&tamanho=20"), Ct))!;
        var busca = (await client.GetFromJsonAsync<PaginaDeLojasDto>(U("/api/estoque/lojas?busca=outl&pagina=1&tamanho=20"), Ct))!;

        lista.Count.ShouldBe(3);
        (pagina.Total, pagina.Itens.Count, pagina.Pagina, pagina.Tamanho).ShouldBe((3, 2, 1, 2));
        inativas.Itens.Select(l => l.Nome).ShouldBe(["Depósito"]);
        busca.Itens.Select(l => l.Nome).ShouldBe(["Outlet"]);
    }

    [Fact]
    public async Task Lojas_SituacaoEmLote_MesmasRegrasDaUnitaria()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        var outlet = await CriarLoja(client, "Outlet");
        await Entrada(client, outlet.Id, new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 1 });
        var vendedor = await Entrar(_api, "vendedor@kamus.local");

        var comEstoque = await client.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = new[] { deposito.Id, outlet.Id }, ativo = false }, Ct);
        comEstoque.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Detalhe(comEstoque)).ShouldContain("Outlet ainda tem estoque");
        (await Lojas(client)).ShouldAllBe(l => l.Ativa); // tudo ou nada

        var todas = await client.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = new[] { centro.Id, deposito.Id }, ativo = false }, Ct);
        todas.StatusCode.ShouldBe(HttpStatusCode.OK, await todas.Content.ReadAsStringAsync(Ct)); // o Outlet continua ativo
        (await todas.Content.ReadFromJsonAsync<AlteradosDto>(Ct))!.Alterados.ShouldBe(2);
        (await Lojas(client)).Where(l => l.Ativa).Select(l => l.Nome).ShouldBe(["Outlet"]);

        var ultima = await client.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = new[] { outlet.Id }, ativo = false }, Ct);
        ultima.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Detalhe(ultima)).ShouldContain("ao menos uma loja ativa");

        (await client.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = new[] { centro.Id, deposito.Id }, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Lojas(client)).ShouldAllBe(l => l.Ativa);
        (await client.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = Array.Empty<Guid>(), ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await vendedor.PostAsJsonAsync(U("/api/estoque/lojas/situacao"), new { ids = new[] { deposito.Id }, ativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Lojas_RelatorioComFiltrosOuSoAsSelecionadas()
    {
        var client = await Entrar(_api);
        await Lojas(client);
        var deposito = await CriarLoja(client, "Depósito");

        var tudo = await client.GetStringAsync(U("/api/relatorios/lojas.csv"), Ct);
        var selecionada = await client.GetStringAsync(U($"/api/relatorios/lojas.csv?ids={deposito.Id}"), Ct);

        tudo.ShouldContain("Kamus Moda Centro");
        tudo.ShouldContain("Depósito");
        selecionada.ShouldContain("Depósito");
        selecionada.ShouldNotContain("Kamus Moda Centro");
        var dados = await client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/lojas/dados?ids={deposito.Id}"), Ct);
        dados.GetRawText().ShouldContain("Selecionados");
    }

    [Fact]
    public async Task Posicao_PaginadaMantemOsTotaisDeTodasAsLinhas_ERelatorioSoDosSelecionados()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var regata = await CriarCamiseta(client, "REG-001");
        var loja = (await Lojas(client)).Single();
        await Entrada(client, loja.Id, new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 2, custoUnitario = 20m },
            new { skuId = Sku(regata, "Preto", "P").Id, quantidade = 3, custoUnitario = 10m });

        var pagina2 = (await client.GetFromJsonAsync<PosicaoDto>(U("/api/estoque/posicao?pagina=2&tamanho=1"), Ct))!;
        var semPagina = (await client.GetFromJsonAsync<PosicaoDto>(U("/api/estoque/posicao"), Ct))!;

        (pagina2.Total, pagina2.Pagina, pagina2.Tamanho).ShouldBe((2, 2, 1));
        pagina2.Itens.Select(i => i.Referencia).ShouldBe(["REG-001"]);
        (pagina2.TotalDePecas, pagina2.ValorEmCusto).ShouldBe((5, 70m));
        semPagina.Itens.Count.ShouldBe(2);

        var csv = await client.GetStringAsync(U($"/api/relatorios/posicao-de-estoque.csv?ids={regata.Id}"), Ct);
        csv.ShouldContain("REG-001");
        csv.ShouldNotContain("CAM-001");
    }

    private static async Task<Guid> ImportarNota(HttpClient client, string numero, string chave)
    {
        using var form = new MultipartFormDataContent();
        var conteudo = new ByteArrayContent(Encoding.UTF8.GetBytes(ExemploDeNfe.Xml([Camisetas], chave: chave, numero: numero)));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(conteudo, "arquivo", "nota.xml");
        var resposta = await client.PostAsync(U("/api/estoque/notas"), form, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<ResumoDaNotaDto>(Ct))!.Id;
    }

    [Fact]
    public async Task Notas_FiltrosPeriodoEPaginacao_ERelatorio()
    {
        var client = await Entrar(_api);
        var primeira = await ImportarNota(client, "1234", ExemploDeNfe.Chave);
        var segunda = await ImportarNota(client, "9999", "35261011222333000181550010000099991000099990");
        (await client.PostAsync(U($"/api/estoque/notas/{segunda}/descartar"), null, Ct)).EnsureSuccessStatusCode();
        await ImportarNota(client, "5555", "35261011222333000181550010000055551000055550");

        var pendentes = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?situacao=pendentes&pagina=1&tamanho=1"), Ct))!;
        var descartadas = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?situacao=descartadas&pagina=1&tamanho=20"), Ct))!;
        var busca = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?busca=aurora&pagina=1&tamanho=20"), Ct))!;
        var porNumero = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?busca=1234&pagina=1&tamanho=20"), Ct))!;
        var noPeriodo = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?de=2026-10-10&ate=2026-10-10&pagina=1&tamanho=20"), Ct))!;
        var foraDoPeriodo = (await client.GetFromJsonAsync<PaginaDeNotasDto>(U("/api/estoque/notas?de=2026-10-11&pagina=1&tamanho=20"), Ct))!;
        var semPagina = (await client.GetFromJsonAsync<List<ResumoDaNotaDto>>(U("/api/estoque/notas"), Ct))!;

        (pendentes.Total, pendentes.Itens.Count).ShouldBe((2, 1));
        descartadas.Itens.Select(n => n.Id).ShouldBe([segunda]);
        busca.Total.ShouldBe(2); // "todas" por padrão = sem as descartadas
        porNumero.Itens.Select(n => n.Id).ShouldBe([primeira]);
        noPeriodo.Total.ShouldBe(2);
        foraDoPeriodo.Total.ShouldBe(0);
        semPagina.Count.ShouldBe(2);

        var csv = await client.GetStringAsync(U("/api/relatorios/notas-de-entrada.csv?situacao=pendentes"), Ct);
        csv.ShouldContain("1234");
        csv.ShouldContain("5555");
        csv.ShouldContain("Aurora Moda");
        var selecionada = await client.GetStringAsync(U($"/api/relatorios/notas-de-entrada.csv?ids={segunda}"), Ct);
        selecionada.ShouldContain("9999");
        selecionada.ShouldNotContain("1234");
    }

    [Fact]
    public async Task Inventarios_FiltrosEPaginacao_ERelatorio()
    {
        var client = await Entrar(_api);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        var primeiro = (await (await client.PostAsJsonAsync(U("/api/estoque/inventarios"), new { lojaId = centro.Id }, Ct)).Content.ReadFromJsonAsync<InventarioDto>(Ct))!;
        (await client.PostAsync(U($"/api/estoque/inventarios/{primeiro.Id}/cancelar"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(U("/api/estoque/inventarios"), new { lojaId = centro.Id }, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(U("/api/estoque/inventarios"), new { lojaId = deposito.Id }, Ct)).EnsureSuccessStatusCode();
        var hoje = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var abertos = (await client.GetFromJsonAsync<PaginaDeInventariosDto>(U("/api/estoque/inventarios?situacao=Aberto&pagina=1&tamanho=1"), Ct))!;
        var doDeposito = (await client.GetFromJsonAsync<PaginaDeInventariosDto>(U($"/api/estoque/inventarios?lojaId={deposito.Id}&pagina=1&tamanho=20"), Ct))!;
        var hojeEmDiante = (await client.GetFromJsonAsync<PaginaDeInventariosDto>(U($"/api/estoque/inventarios?de={hoje}&ate={hoje}&pagina=1&tamanho=20"), Ct))!;
        var futuro = (await client.GetFromJsonAsync<PaginaDeInventariosDto>(U("/api/estoque/inventarios?de=2099-01-01&pagina=1&tamanho=20"), Ct))!;
        var semPagina = (await client.GetFromJsonAsync<List<InventarioDto>>(U("/api/estoque/inventarios"), Ct))!;

        (abertos.Total, abertos.Itens.Count).ShouldBe((2, 1));
        doDeposito.Itens.Single().Loja.ShouldBe("Depósito");
        hojeEmDiante.Total.ShouldBe(3);
        futuro.Total.ShouldBe(0);
        semPagina.Count.ShouldBe(3);

        var csv = await client.GetStringAsync(U("/api/relatorios/inventarios.csv?situacao=Cancelado"), Ct);
        csv.ShouldContain("Cancelado");
        csv.ShouldNotContain("Em contagem");
        var selecionado = await client.GetStringAsync(U($"/api/relatorios/inventarios.csv?ids={primeiro.Id}"), Ct);
        selecionado.ShouldContain("Cancelado");
        selecionado.ShouldNotContain("Depósito");
    }
}
