using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Registro de venda (baixa o estoque e lança no financeiro), consulta, cancelamento e comissões.</summary>
internal static class VendasEndpoints
{
    public const string Origem = "Venda";
    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    public sealed record ItemRequest(Guid SkuId, int Quantidade, decimal Desconto);
    public sealed record PagamentoRequest(Guid FormaId, decimal Valor, int Parcelas);
    public sealed record VendaRequest(Guid LojaId, DateOnly? Data, Guid? ClienteId, string? ClienteNome, Guid? VendedorId, IReadOnlyList<ItemRequest>? Itens,
        decimal DescontoGeral, IReadOnlyList<PagamentoRequest>? Pagamentos, string? NotaExterna, string? Observacao);
    public sealed record CancelarRequest(string? Motivo);

    public sealed record ItemDto(Guid SkuId, Guid ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Desconto, decimal Total, decimal? CustoUnitario);
    public sealed record PagamentoDto(Guid FormaId, string FormaNome, string Tipo, decimal Valor, int Parcelas, decimal TaxaPercentual, decimal ValorTaxa);

    /// <param name="CustoTotal">Custo e lucro só para quem vê custos (Dono, Gerente e Financeiro).</param>
    /// <param name="Avisos">Itens que ficaram com estoque negativo.</param>
    public sealed record VendaDto(Guid Id, Guid EmpresaId, Guid LojaId, int Numero, DateOnly Data, Guid? ClienteId, string? ClienteNome, Guid? VendedorId, string? VendedorNome,
        decimal ComissaoPercentual, decimal Comissao, decimal Subtotal, decimal DescontoGeral, decimal Total, decimal Troco, int Pecas, decimal TaxaTotal,
        decimal? CustoTotal, decimal? LucroBruto, string Status, string? NotaExterna, string? Observacao, string? RegistradaPor, DateTimeOffset RegistradaEm,
        string? MotivoDoCancelamento, string? CanceladaPor, DateTimeOffset? CanceladaEm, IReadOnlyList<ItemDto> Itens, IReadOnlyList<PagamentoDto> Pagamentos,
        IReadOnlyList<string> Avisos);

    public static IEndpointRouteBuilder MapVendas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/", Listar).WithSummary("Vendas com totais. Filtros: de, ate, vendedorId, status (concluidas, canceladas, todas), busca (nº, cliente, nota)");
        g.MapGet("/comissoes", Comissoes).WithSummary("Comissão por vendedor sobre as vendas concluídas no período");
        g.MapGet("/{id:guid}", Obter).WithSummary("Venda com itens e pagamentos");
        g.MapPost("/", Registrar).WithSummary("Registra a venda: baixa o estoque, lança dinheiro/PIX no caixa e gera os recebíveis de cartão")
            .Produces<VendaDto>(StatusCodes.Status201Created);
        g.MapPost("/{id:guid}/cancelar", Cancelar).WithSummary("Cancela com motivo: devolve o estoque e estorna o financeiro (Dono/Gerente)");
        return app;
    }

    private static VendaDto Dto(Venda v, bool verCusto, IReadOnlyList<string>? avisos = null) => new(v.Id, v.EmpresaId, v.LojaId, v.Numero, v.Data, v.ClienteId, v.ClienteNome,
        v.VendedorId, v.VendedorNome, v.ComissaoPercentual, v.Comissao, v.Subtotal, v.DescontoGeral, v.Total, v.Troco, v.Pecas, v.TaxaTotal,
        verCusto ? v.CustoTotal : null, verCusto ? v.LucroBruto : null, v.Status.ToString(), v.NotaExterna, v.Observacao, v.RegistradaPor, v.RegistradaEm,
        v.MotivoDoCancelamento, v.CanceladaPor, v.CanceladaEm,
        v.Itens.Select(i => new ItemDto(i.SkuId, i.ProdutoId, i.Descricao, i.Quantidade, i.PrecoUnitario, i.Desconto, i.Total, verCusto ? i.CustoUnitario : null)).ToList(),
        v.Pagamentos.Select(p => new PagamentoDto(p.FormaId, p.FormaNome, p.Tipo.ToString(), p.Valor, p.Parcelas, p.TaxaPercentual, p.ValorTaxa)).ToList(),
        avisos ?? []);

    private static FiltroDeVendas? Filtro(DateOnly? de, DateOnly? ate, Guid? vendedorId, string? status, string? busca)
    {
        var s = status?.ToLowerInvariant() ?? "todas";
        return Consultas.Status.Contains(s) ? new FiltroDeVendas(de, ate, vendedorId, s, busca) : null;
    }

    private static async Task<IResult> Listar(DateOnly? de, DateOnly? ate, Guid? vendedorId, string? status, string? busca, VendasDbContext db, IEmpresaAtiva empresa,
        IUsuarioAtual usuario, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, [.. Permissoes.Registrar, .. Permissoes.Consultar], "Seu perfil não acessa as vendas.") is { } negado) return negado;
        if (Filtro(de, ate, vendedorId, status, busca) is not { } filtro) return Permissoes.Erro("Status inválido: concluidas, canceladas ou todas.");
        return Results.Ok(await Consultas.ListarAsync(db, empresa, usuario.Id, filtro, ct));
    }

    private static async Task<IResult> Comissoes(DateOnly? de, DateOnly? ate, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Consultar, "Só Dono, Gerente e Financeiro veem as comissões.") is { } negado) return negado;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        return Results.Ok(await Consultas.ComissoesAsync(db, empresa, inicio, ate ?? hoje, ct));
    }

    private static async Task<IResult> Obter(Guid id, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, CancellationToken ct)
    {
        var venda = await Consultas.Visiveis(db, empresa, usuario.Id).SingleOrDefaultAsync(v => v.Id == id, ct);
        return venda is null ? Results.NotFound() : Results.Ok(Dto(venda, Permissoes.VeTudo(empresa)));
    }

    private static async Task<IResult> Registrar(VendaRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, ICatalogoDeProdutos catalogo,
        IDiretorioDeVendedores vendedores, IMovimentacaoDeEstoque estoque, IContasAReceber financeiro, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Registrar, "Só Dono, Gerente e Vendedor registram vendas.") is { } negado) return negado;
        var empresaId = empresa.EmpresaId!.Value;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var data = r.Data ?? hoje;
        if (data > hoje) return Permissoes.Erro("A data da venda não pode ser futura.");
        var itens = r.Itens ?? [];
        var pedidos = r.Pagamentos ?? [];
        if (pedidos.Count == 0) return Permissoes.Erro("Informe ao menos uma forma de pagamento.");

        VendedorResumo? vendedor = null;
        if (r.VendedorId is { } vendedorId)
        {
            vendedor = await vendedores.ObterAsync(vendedorId, ct);
            if (vendedor is null) return Permissoes.Erro("Vendedor não encontrado.");
            if (!vendedor.Ativo) return Permissoes.Erro($"{vendedor.Nome} está inativo.");
        }

        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (itens.Any(i => !skus.ContainsKey(i.SkuId))) return Permissoes.Erro("Há itens que não existem no catálogo.");
        if (itens.FirstOrDefault(i => !skus[i.SkuId].Ativo) is { } inativo) return Permissoes.Erro($"{Nome(skus[inativo.SkuId])} está inativo no catálogo.");

        var formaIds = pedidos.Select(p => p.FormaId).Distinct().ToList();
        var formas = await db.Formas.AsNoTracking().Where(f => formaIds.Contains(f.Id) && f.EmpresaId == empresaId).ToDictionaryAsync(f => f.Id, ct);
        var pagamentos = new List<(PagamentoNovo Pagamento, FormaDePagamento Forma)>();
        foreach (var p in pedidos)
        {
            if (!formas.TryGetValue(p.FormaId, out var forma)) return Permissoes.Erro("Forma de pagamento não encontrada.");
            if (!forma.Ativa) return Permissoes.Erro($"{forma.Nome} está desativada.");
            if (p.Parcelas > forma.MaxParcelas)
                return Permissoes.Erro(forma.MaxParcelas == 1 ? $"{forma.Nome} não parcela." : $"{forma.Nome} aceita até {forma.MaxParcelas} parcelas.");
            if (forma.EntraNaHora && (forma.ContaId is not { } conta || !await financeiro.ContaValidaAsync(empresaId, conta, ct)))
                return Permissoes.Erro($"Configure a conta financeira de {forma.Nome} em Formas de pagamento.");
            pagamentos.Add((new PagamentoNovo(forma.Id, forma.Nome, forma.Tipo, p.Valor, p.Parcelas, forma.Taxa(p.Parcelas)), forma));
        }

        var numero = (await db.Vendas.Where(v => v.EmpresaId == empresaId).MaxAsync(v => (int?)v.Numero, ct) ?? 0) + 1;
        Venda venda;
        try
        {
            venda = Venda.Criar(
                new DadosDaVenda(empresaId, r.LojaId, numero, data, r.ClienteId, r.ClienteNome, vendedor?.Id, vendedor?.Nome, vendedor?.ComissaoPercentual ?? 0,
                    r.NotaExterna, r.Observacao, usuario.Id),
                itens.Select(i => new ItemNovo(i.SkuId, skus[i.SkuId].ProdutoId, Nome(skus[i.SkuId]), i.Quantidade, skus[i.SkuId].PrecoVenda, i.Desconto)).ToList(),
                r.DescontoGeral, pagamentos.Select(p => p.Pagamento).ToList(), usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }

        var documento = $"Venda nº {numero}";
        var configuracao = await FormasEndpoints.ConfiguracaoAsync(db, empresaId, ct);
        SaidaRegistrada saida;
        try
        {
            saida = await estoque.RegistrarVendaAsync(new SaidaDeEstoque(empresaId, r.LojaId, documento,
                venda.Itens.Select(i => new ItemDeSaida(i.SkuId, i.Quantidade)).ToList(), configuracao.PermitirEstoqueNegativo), ct);
        }
        catch (SaidaDeEstoqueRecusadaException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }
        venda.RegistrarBaixaDeEstoque(saida.OperacaoId, saida.Itens.ToDictionary(i => i.SkuId, i => i.CustoUnitario));

        try
        {
            await financeiro.RegistrarVendaAsync(Recebiveis(venda, documento, formas), ct);
            db.Vendas.Add(venda);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sem transação entre módulos: desfaz o que já foi gravado nos outros contextos.
            await estoque.EstornarVendaAsync(saida.OperacaoId, $"{documento} não concluída", CancellationToken.None);
            await financeiro.CancelarOrigemAsync(Origem, venda.Id.ToString(), usuario.Nome, CancellationToken.None);
            throw;
        }

        var avisos = saida.Itens.Where(i => i.SaldoApos < 0)
            .Select(i => $"{Nome(skus[i.SkuId])} ficou com estoque {i.SaldoApos}.").ToList();
        return Results.Created($"/api/vendas/{venda.Id}", Dto(venda, Permissoes.VeTudo(empresa), avisos));
    }

    private static string Nome(SkuDoCatalogo sku) => $"{sku.Descricao} {sku.Cor} {sku.Tamanho}";

    private static ContasAReceberDeVenda Recebiveis(Venda venda, string documento, Dictionary<Guid, FormaDePagamento> formas) =>
        new(venda.EmpresaId, Origem, venda.Id.ToString(), venda.ClienteId, venda.ClienteNome, venda.Data,
            venda.Pagamentos.Select(p =>
            {
                var forma = formas[p.FormaId];
                var descricao = $"{documento} — {p.FormaNome}{(p.Parcelas > 1 ? $" {p.Parcelas}x" : "")}";
                return forma.EntraNaHora
                    ? new RecebimentoDeVenda(descricao, [new ParcelaAReceber(venda.Data, p.Valor)], forma.ContaId, JaRecebido: true, null)
                    : new RecebimentoDeVenda(descricao, forma.Recebimentos(p.Valor, p.Parcelas, venda.Data), forma.ContaId, JaRecebido: false,
                        p.ValorTaxa > 0 ? $"Valor bruto R$ {Venda.Moeda(p.Valor)}, taxa {p.TaxaPercentual.ToString("0.##", PtBr)}% (R$ {Venda.Moeda(p.ValorTaxa)})." : null);
            }).ToList());

    private static async Task<IResult> Cancelar(Guid id, CancelarRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        IMovimentacaoDeEstoque estoque, IContasAReceber financeiro, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, "Só Dono e Gerente cancelam vendas.") is { } negado) return negado;
        var venda = await db.Vendas.Include(v => v.Itens).Include(v => v.Pagamentos).SingleOrDefaultAsync(v => v.Id == id && v.EmpresaId == empresa.EmpresaId, ct);
        if (venda is null) return Results.NotFound();
        try
        {
            venda.Cancelar(r.Motivo ?? "", usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }

        if (venda.OperacaoDeEstoqueId is { } operacao) await estoque.EstornarVendaAsync(operacao, $"Cancelamento da venda nº {venda.Numero}", ct);
        await financeiro.CancelarOrigemAsync(Origem, venda.Id.ToString(), usuario.Nome, ct);
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("venda_cancelada", $"Venda nº {venda.Numero} (R$ {Venda.Moeda(venda.Total)}) cancelada: {venda.MotivoDoCancelamento}", ct: ct);
        return Results.Ok(Dto(venda, Permissoes.VeTudo(empresa)));
    }
}
