using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Vendas;

/// <summary>Vendas no PostgreSQL: venda com troco e cartão parcelado, cancelamento e RLS nas tabelas do schema vendas.</summary>
[Collection(PostgresGroup.Name)]
public sealed class VendasPostgresTests(PostgresFixture db)
{
    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record ContaDto(Guid Id, decimal Saldo);
    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record VendaDto(Guid Id, int Numero, decimal Total, decimal Troco, string Status);
    private sealed record CelulaDto(Guid SkuId, int Quantidade);
    private sealed record MatrizDto(List<CelulaDto> Celulas);

    [Fact]
    public async Task Vendas_NoPostgres_RegistrarCancelarERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(U("/api/auth/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.NomeFantasia == "Kamus Moda Centro").Id.ToString());
        var hoje = Kamus.SharedKernel.Tempo.CalendarioBrasilia.Hoje(TimeProvider.System);
        var sufixo = Guid.NewGuid().ToString()[..6];

        var camiseta = await CriarCamiseta(client, $"VND-{sufixo}");
        var azulM = Sku(camiseta, "Azul", "M").Id;
        var loja = (await Lojas(client)).First(l => l.Ativa);
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 3, custoUnitario = 20m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = $"Caixa {sufixo}", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = hoje }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var formas = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!;
        var dinheiro = formas.First(f => f.Tipo == "Dinheiro");
        (await client.PutAsJsonAsync(U($"/api/vendas/formas/{dinheiro.Id}"),
            new { dinheiro.Nome, dinheiro.Tipo, contaId = caixa.Id, dinheiro.TaxaAVista, dinheiro.TaxaParcelado, dinheiro.MaxParcelas, dinheiro.PrazoDias }, Ct)).EnsureSuccessStatusCode();
        var credito = formas.First(f => f.Tipo == "Credito");

        var r = await client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = loja.Id,
            itens = new[] { new { skuId = azulM, quantidade = 2, desconto = 0m } },
            descontoGeral = 0m,
            pagamentos = new object[] { new { formaId = dinheiro.Id, valor = 70m, parcelas = 1 }, new { formaId = credito.Id, valor = 59.90m, parcelas = 3 } },
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var venda = (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
        (venda.Total, venda.Troco).ShouldBe((119.80m, 10.10m));

        async Task<int> Saldo() => (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{camiseta.Id}"), Ct))!.Celulas.Single(c => c.SkuId == azulM).Quantidade;
        async Task<decimal> Caixa() => (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single(c => c.Id == caixa.Id).Saldo;
        ((await Saldo()), (await Caixa())).ShouldBe((1, 59.90m));

        var c = await client.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = "Teste" }, Ct);
        c.StatusCode.ShouldBe(HttpStatusCode.OK, await c.Content.ReadAsStringAsync(Ct));
        ((await Saldo()), (await Caixa())).ShouldBe((3, 0m));

        // Troca sem levar nada: a peça volta ao estoque e o crédito vira vale-troca
        var outra = (await (await client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = loja.Id,
            itens = new[] { new { skuId = azulM, quantidade = 1, desconto = 0m } },
            descontoGeral = 0m,
            pagamentos = new object[] { new { formaId = dinheiro.Id, valor = 59.90m, parcelas = 1 } },
        }, Ct)).Content.ReadFromJsonAsync<VendaDto>(Ct))!;
        var troca = await client.PostAsJsonAsync(U("/api/vendas/trocas"), new
        {
            vendaId = outra.Id,
            motivo = "Defeito",
            devolvidos = new[] { new { skuId = azulM, quantidade = 1, destino = "Estoque" } },
            levados = Array.Empty<object>(),
            pagamentos = Array.Empty<object>(),
        }, Ct);
        troca.StatusCode.ShouldBe(HttpStatusCode.Created, await troca.Content.ReadAsStringAsync(Ct));
        var vale = (await troca.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("vale");
        vale.GetProperty("saldo").GetDecimal().ShouldBe(59.90m);
        (await Saldo()).ShouldBe(3);

        // Condicional: abre (peça sai do estoque) e fecha devolvendo tudo
        var cliente = await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = $"Cliente {sufixo}", ehCliente = true, ehFornecedor = false, aceitaMarketing = false }, Ct);
        var clienteId = (await cliente.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("id").GetGuid();
        var aberta = await client.PostAsJsonAsync(U("/api/vendas/condicionais"), new
        {
            lojaId = loja.Id,
            clienteId,
            clienteNome = $"Cliente {sufixo}",
            itens = new[] { new { skuId = azulM, quantidade = 1 } },
            liberar = true,
        }, Ct);
        aberta.StatusCode.ShouldBe(HttpStatusCode.Created, await aberta.Content.ReadAsStringAsync(Ct));
        var condicionalId = (await aberta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("id").GetGuid();
        (await Saldo()).ShouldBe(2);
        var fechada = await client.PostAsJsonAsync(U($"/api/vendas/condicionais/{condicionalId}/fechar"),
            new { devolvidos = new[] { new { skuId = azulM, quantidade = 1 } }, descontoGeral = 0m }, Ct);
        fechada.StatusCode.ShouldBe(HttpStatusCode.OK, await fechada.Content.ReadAsStringAsync(Ct));
        (await Saldo()).ShouldBe(3);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        foreach (var tabela in new[] { "formas_de_pagamento", "configuracoes", "vendas", "itens_da_venda", "pagamentos_da_venda", "trocas", "itens_devolvidos",
            "itens_levados", "pagamentos_da_troca", "vales_troca", "fechamentos_de_comissao", "condicionais", "itens_da_condicional" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM vendas.{tabela}", conn);
            ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0, $"RLS em vendas.{tabela}");
        }
    }
}
