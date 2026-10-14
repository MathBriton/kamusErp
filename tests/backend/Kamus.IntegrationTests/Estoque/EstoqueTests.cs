using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Lojas, entradas, ajustes, transferências, custo médio, posição em matriz e kardex.</summary>
public sealed class EstoqueTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record GradeDto(Guid Id, string Nome);
    private sealed record CorDto(Guid Id, string Nome);
    private sealed record SkuDto(Guid Id, Guid CorId, string Tamanho);
    private sealed record ProdutoDto(Guid Id, List<CorDto> Cores, List<SkuDto> Skus);
    private sealed record LojaDto(Guid Id, Guid EmpresaId, string Nome, bool Ativa, int Quantidade);
    private sealed record OperacaoDto(Guid OperacaoId, int Itens, int Quantidade);
    private sealed record CelulaDto(Guid SkuId, Guid CorId, string Tamanho, int Quantidade, decimal? CustoMedio);
    private sealed record LojaDoProdutoDto(Guid LojaId, string Nome, int Quantidade);
    private sealed record MatrizDto(Guid ProdutoId, string Referencia, List<string> Tamanhos, List<CorDto> Cores, List<CelulaDto> Celulas, List<LojaDoProdutoDto> Lojas, int Total);
    private sealed record ItemDaPosicaoDto(Guid ProdutoId, string Referencia, string Descricao, int Quantidade, decimal? ValorEmCusto, decimal ValorEmVenda);
    private sealed record PosicaoDto(List<ItemDaPosicaoDto> Itens, int TotalDePecas, decimal? ValorEmCusto, decimal ValorEmVenda);
    private sealed record MovimentoDto(string Tipo, string Loja, string? LojaRelacionada, string Cor, string Tamanho, int Quantidade, decimal? CustoUnitario,
        int SaldoApos, decimal? CustoMedioApos, string? Motivo, string? Documento, string? Usuario);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private sealed record Sessao(HttpClient Client, List<EmpresaDto> Empresas);

    private async Task<Sessao> Entrar(string email = "admin@kamus.local", string empresa = "Kamus Moda Centro")
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha = Senha }, Ct);
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.NomeFantasia == empresa).Id.ToString());
        return new Sessao(client, sessao.Empresas);
    }

    private async Task<HttpClient> Admin(string empresa = "Kamus Moda Centro") => (await Entrar(empresa: empresa)).Client;

    private static async Task<ProdutoDto> CriarCamiseta(HttpClient client, string referencia = "CAM-001", decimal? custo = 24m)
    {
        (await client.PostAsync(U("/api/grades/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/cores/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var grades = (await client.GetFromJsonAsync<List<GradeDto>>(U("/api/grades"), Ct))!;
        var cores = (await client.GetFromJsonAsync<List<CorDto>>(U("/api/cores"), Ct))!;
        var resposta = await client.PostAsJsonAsync(U("/api/produtos"), new
        {
            referencia,
            descricao = $"Camiseta {referencia}",
            precoVenda = 59.90m,
            precoCusto = custo,
            gradeId = grades.Single(g => g.Nome == "PP ao GG").Id,
            corIds = cores.Where(c => c.Nome is "Azul" or "Preto").Select(c => c.Id),
        }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>(Ct))!;
    }

    private static Guid Sku(ProdutoDto p, string cor, string tamanho) =>
        p.Skus.Single(s => s.CorId == p.Cores.Single(c => c.Nome == cor).Id && s.Tamanho == tamanho).Id;

    private static async Task<List<LojaDto>> Lojas(HttpClient client) => (await client.GetFromJsonAsync<List<LojaDto>>(U("/api/estoque/lojas"), Ct))!;

    private static async Task<LojaDto> CriarLoja(HttpClient client, string nome)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<LojaDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> Entrada(HttpClient client, Guid lojaId, params object[] itens) =>
        client.PostAsJsonAsync(U("/api/estoque/entradas"), new { lojaId, documento = "NF 123", itens }, Ct);

    private static async Task<OperacaoDto> EntradaOk(HttpClient client, Guid lojaId, params object[] itens)
    {
        var resposta = await Entrada(client, lojaId, itens);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<OperacaoDto>(Ct))!;
    }

    private static async Task<MatrizDto> Matriz(HttpClient client, Guid produtoId, Guid? lojaId = null) =>
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produtoId}{(lojaId is null ? "" : $"?lojaId={lojaId}")}"), Ct))!;

    private static int Qtd(MatrizDto m, Guid skuId) => m.Celulas.Single(c => c.SkuId == skuId).Quantidade;

    private static async Task<string> Detalhe(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!;

    [Fact]
    public async Task Lojas_PrimeiroAcessoCriaALojaDaEmpresa()
    {
        var client = await Admin();

        var lojas = await Lojas(client);
        var deNovo = await Lojas(client);

        lojas.Count.ShouldBe(1);
        lojas[0].Nome.ShouldBe("Kamus Moda Centro");
        lojas[0].Ativa.ShouldBeTrue();
        deNovo.Single().Id.ShouldBe(lojas[0].Id);
    }

    [Fact]
    public async Task Lojas_DonoCriaERenomeia_NomeRepetido409_VendedorNao()
    {
        var client = await Admin();
        var vendedor = (await Entrar("vendedor@kamus.local")).Client;

        var loja = await CriarLoja(client, "Depósito");
        (await client.PutAsJsonAsync(U($"/api/estoque/lojas/{loja.Id}"), new { nome = "Depósito Central" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = "depósito central" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = " " }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await vendedor.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = "Outra" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await Lojas(client)).Select(l => l.Nome).ShouldBe(["Depósito Central", "Kamus Moda Centro"], ignoreOrder: true);
    }

    [Fact]
    public async Task Lojas_AcimaDoLimiteDaLicenca_409()
    {
        var client = await Admin();
        await Lojas(client);
        await using (var escopo = _factory.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
            (await db.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct)).DefinirLimites(null, 1, null);
            await db.SaveChangesAsync(Ct);
            escopo.ServiceProvider.GetRequiredService<CacheDeLicencas>().Invalidar(TenantDemo);
        }

        var resposta = await client.PostAsJsonAsync(U("/api/estoque/lojas"), new { nome = "Depósito" }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("codigo").GetString().ShouldBe("LIMITE_DA_LICENCA");
    }

    [Fact]
    public async Task Entrada_SomaSaldoCalculaCustoMedioEMostraNaMatriz()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M");
        var pretoG = Sku(camiseta, "Preto", "G");

        var operacao = await EntradaOk(client, loja.Id, new { skuId = azulM, quantidade = 10, custoUnitario = 20m }, new { skuId = pretoG, quantidade = 4 });
        await EntradaOk(client, loja.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 26m });

        operacao.ShouldBe(operacao with { Itens = 2, Quantidade = 14 });
        var matriz = await Matriz(client, camiseta.Id);
        matriz.Tamanhos.ShouldBe(["PP", "P", "M", "G", "GG"]);
        matriz.Cores.Select(c => c.Nome).ShouldBe(["Azul", "Preto"], ignoreOrder: true);
        matriz.Celulas.Count.ShouldBe(10);
        Qtd(matriz, azulM).ShouldBe(15);
        matriz.Celulas.Single(c => c.SkuId == azulM).CustoMedio.ShouldBe(22m);
        Qtd(matriz, pretoG).ShouldBe(4);
        matriz.Celulas.Single(c => c.SkuId == pretoG).CustoMedio.ShouldBe(24m); // sem custo na entrada: custo do produto
        matriz.Total.ShouldBe(19);
        matriz.Lojas.Single().ShouldBe(new LojaDoProdutoDto(loja.Id, "Kamus Moda Centro", 19));
    }

    [Fact]
    public async Task Entrada_Invalida_400SemGravarNada()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M");

        (await Entrada(client, loja.Id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 1 }, new { skuId = Guid.NewGuid(), quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 0 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 1 }, new { skuId = azulM, quantidade = 2 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Entrada(client, Guid.NewGuid(), new { skuId = azulM, quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await Matriz(client, camiseta.Id)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Entrada_LojaDeOutraEmpresa_404()
    {
        var centro = await Admin();
        var shopping = await Admin("Kamus Moda Shopping");
        var camiseta = await CriarCamiseta(centro);
        var lojaDoShopping = (await Lojas(shopping)).Single();

        (await Entrada(centro, lojaDoShopping.Id, new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ajuste_ComMotivo_BaixaOuSoma_SemDeixarNegativo()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M");
        await EntradaOk(client, loja.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 20m });

        (await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = loja.Id, motivo = "Avaria", itens = new[] { new { skuId = azulM, quantidade = -2 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = loja.Id, motivo = "Correcao", itens = new[] { new { skuId = azulM, quantidade = 1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = loja.Id, itens = new[] { new { skuId = azulM, quantidade = -1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = loja.Id, motivo = "Outro", itens = new[] { new { skuId = azulM, quantidade = -1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // "Outro" exige observação
        var demais = await client.PostAsJsonAsync(U("/api/estoque/ajustes"), new { lojaId = loja.Id, motivo = "Furto", itens = new[] { new { skuId = azulM, quantidade = -5 } } }, Ct);

        demais.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Detalhe(demais)).ShouldContain("há 4");
        var matriz = await Matriz(client, camiseta.Id);
        Qtd(matriz, azulM).ShouldBe(4);
        matriz.Celulas.Single(c => c.SkuId == azulM).CustoMedio.ShouldBe(20m);

        var historico = await client.GetStringAsync(U("/api/auditoria"), Ct);
        historico.ShouldContain("ajuste_de_estoque");
    }

    [Fact]
    public async Task Transferencia_MoveSaldoECustoEntreLojas()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        var azulM = Sku(camiseta, "Azul", "M");
        await EntradaOk(client, centro.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 20m });

        var resposta = await client.PostAsJsonAsync(U("/api/estoque/transferencias"),
            new { origemId = centro.Id, destinoId = deposito.Id, itens = new[] { new { skuId = azulM, quantidade = 3 } } }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        Qtd(await Matriz(client, camiseta.Id, centro.Id), azulM).ShouldBe(2);
        var noDeposito = await Matriz(client, camiseta.Id, deposito.Id);
        Qtd(noDeposito, azulM).ShouldBe(3);
        noDeposito.Celulas.Single(c => c.SkuId == azulM).CustoMedio.ShouldBe(20m);
        var todas = await Matriz(client, camiseta.Id);
        Qtd(todas, azulM).ShouldBe(5);
        todas.Lojas.Select(l => (l.Nome, l.Quantidade)).ShouldBe([("Depósito", 3), ("Kamus Moda Centro", 2)], ignoreOrder: true);

        var kardex = (await client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={azulM}"), Ct))!;
        kardex.Select(m => (m.Tipo, m.Loja, m.Quantidade, m.SaldoApos)).ShouldBe(
        [
            ("TransferenciaEntrada", "Depósito", 3, 3),
            ("TransferenciaSaida", "Kamus Moda Centro", -3, 2),
            ("Entrada", "Kamus Moda Centro", 5, 5),
        ]);
        kardex[0].LojaRelacionada.ShouldBe("Kamus Moda Centro");
        kardex[2].ShouldBe(kardex[2] with { Cor = "Azul", Tamanho = "M", Documento = "NF 123", CustoUnitario = 20m, CustoMedioApos = 20m });
        kardex[2].Usuario.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Transferencia_Invalida()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        var azulM = Sku(camiseta, "Azul", "M");
        await EntradaOk(client, centro.Id, new { skuId = azulM, quantidade = 1 });

        (await client.PostAsJsonAsync(U("/api/estoque/transferencias"), new { origemId = centro.Id, destinoId = centro.Id, itens = new[] { new { skuId = azulM, quantidade = 1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/estoque/transferencias"), new { origemId = centro.Id, destinoId = deposito.Id, itens = new[] { new { skuId = azulM, quantidade = 2 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Matriz(client, camiseta.Id, deposito.Id)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Posicao_PorProdutoComValoresEFiltros()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var regata = await CriarCamiseta(client, "REG-001", custo: null);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        await EntradaOk(client, centro.Id, new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 2, custoUnitario = 20m });
        await EntradaOk(client, deposito.Id, new { skuId = Sku(regata, "Preto", "P"), quantidade = 3, custoUnitario = 10m });

        var tudo = (await client.GetFromJsonAsync<PosicaoDto>(U("/api/estoque/posicao"), Ct))!;
        var soCentro = (await client.GetFromJsonAsync<PosicaoDto>(U($"/api/estoque/posicao?lojaId={centro.Id}"), Ct))!;
        var busca = (await client.GetFromJsonAsync<PosicaoDto>(U("/api/estoque/posicao?busca=reg-0"), Ct))!;

        tudo.TotalDePecas.ShouldBe(5);
        tudo.ValorEmCusto.ShouldBe(70m);
        tudo.ValorEmVenda.ShouldBe(299.50m);
        tudo.Itens.Single(i => i.Referencia == "CAM-001").ShouldBe(new ItemDaPosicaoDto(camiseta.Id, "CAM-001", "Camiseta CAM-001", 2, 40m, 119.80m));
        soCentro.Itens.Select(i => i.Referencia).ShouldBe(["CAM-001"]);
        busca.Itens.Select(i => i.Referencia).ShouldBe(["REG-001"]);
    }

    [Fact]
    public async Task Vendedor_VePosicaoSemCusto_NaoMovimentaNemVeKardex()
    {
        var admin = await Admin();
        var vendedor = (await Entrar("vendedor@kamus.local")).Client;
        var camiseta = await CriarCamiseta(admin);
        var loja = (await Lojas(admin)).Single();
        var azulM = Sku(camiseta, "Azul", "M");
        await EntradaOk(admin, loja.Id, new { skuId = azulM, quantidade = 2, custoUnitario = 20m });

        var posicao = (await vendedor.GetFromJsonAsync<PosicaoDto>(U("/api/estoque/posicao"), Ct))!;
        var matriz = await Matriz(vendedor, camiseta.Id);

        posicao.TotalDePecas.ShouldBe(2);
        posicao.ValorEmCusto.ShouldBeNull();
        posicao.Itens.Single().ValorEmCusto.ShouldBeNull();
        matriz.Celulas.Single(c => c.SkuId == azulM).CustoMedio.ShouldBeNull();
        (await Entrada(vendedor, loja.Id, new { skuId = azulM, quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.GetAsync(U($"/api/estoque/kardex?skuId={azulM}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DesativarLoja_ComSaldoOuUltima409_SemSaldoOk()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var centro = (await Lojas(client)).Single();
        var deposito = await CriarLoja(client, "Depósito");
        await EntradaOk(client, deposito.Id, new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 1 });

        (await client.PostAsync(U($"/api/estoque/lojas/{deposito.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await client.PostAsJsonAsync(U("/api/estoque/transferencias"),
            new { origemId = deposito.Id, destinoId = centro.Id, itens = new[] { new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 1 } } }, Ct);
        (await client.PostAsync(U($"/api/estoque/lojas/{deposito.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync(U($"/api/estoque/lojas/{centro.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Entrada(client, deposito.Id, new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsync(U($"/api/estoque/lojas/{deposito.Id}/reativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Relatorio_PosicaoDeEstoqueEmCsv()
    {
        var client = await Admin();
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        await EntradaOk(client, loja.Id, new { skuId = Sku(camiseta, "Azul", "M"), quantidade = 2, custoUnitario = 20m });

        var csv = await client.GetStringAsync(U("/api/relatorios/posicao-de-estoque.csv"), Ct);

        csv.ShouldContain("CAM-001");
        csv.ShouldContain("Kamus Moda Centro");
        csv.ShouldContain("Azul");
    }
}
