using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>Posição de estoque (por produto e em matriz cor × tamanho) e kardex. Sem <c>lojaId</c>, soma as lojas visíveis.</summary>
internal static class PosicaoEndpoints
{
    private const int LimiteDoKardex = 500;

    public sealed record ItemDaPosicaoDto(Guid ProdutoId, string Referencia, string Descricao, int Quantidade, decimal? ValorEmCusto, decimal ValorEmVenda);
    public sealed record PosicaoDto(IReadOnlyList<ItemDaPosicaoDto> Itens, int TotalDePecas, decimal? ValorEmCusto, decimal ValorEmVenda);
    public sealed record CelulaDto(Guid SkuId, Guid CorId, string Tamanho, string CodigoDeBarras, bool Ativo, int Quantidade, decimal? CustoMedio, decimal Preco);
    public sealed record LojaDoProdutoDto(Guid LojaId, string Nome, int Quantidade);
    public sealed record MatrizDto(Guid ProdutoId, string Referencia, string Descricao, decimal PrecoVenda, decimal? PrecoCusto, IReadOnlyList<string> Tamanhos,
        IReadOnlyList<CorDoCatalogo> Cores, IReadOnlyList<CelulaDto> Celulas, IReadOnlyList<LojaDoProdutoDto> Lojas, int Total);
    public sealed record MovimentoDto(Guid Id, DateTimeOffset OcorridoEm, string Tipo, Guid LojaId, string Loja, string? LojaRelacionada, string Referencia,
        string Cor, string Tamanho, int Quantidade, decimal? CustoUnitario, int SaldoApos, decimal? CustoMedioApos, string? Motivo, string? Documento,
        string? Observacao, string? Usuario);

    public static IEndpointRouteBuilder MapPosicao(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque").RequireAuthorization().WithTags("Estoque");
        g.MapGet("/posicao", Posicao).WithSummary("Posição por produto (peças, valor em custo e em venda). Filtros: lojaId, busca");
        g.MapGet("/posicao/{produtoId:guid}", Matriz).WithSummary("Saldo do produto em matriz cor × tamanho, com o total por loja. Filtro: lojaId");
        g.MapGet("/kardex", Kardex).WithSummary($"Extrato de movimentos (mais recentes primeiro, até {LimiteDoKardex}). Filtros: skuId ou produtoId, lojaId");
        return app;
    }

    /// <summary>Lojas consideradas: a pedida (se visível) ou todas as visíveis. Nulo = loja não encontrada.</summary>
    internal static async Task<List<Loja>?> LojasDoFiltroAsync(Lojas lojas, Guid? lojaId, CancellationToken ct)
    {
        var visiveis = await lojas.VisiveisAsync(ct);
        if (lojaId is null) return visiveis;
        var loja = visiveis.FirstOrDefault(l => l.Id == lojaId);
        return loja is null ? null : [loja];
    }

    internal static bool Corresponde(SkuDoCatalogo sku, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca)) return true;
        var termo = TextoParaBusca.Normalizar(busca);
        return TextoParaBusca.Normalizar($"{sku.Referencia} {sku.Descricao}").Contains(termo, StringComparison.Ordinal) || sku.CodigoDeBarras == busca.Trim();
    }

    private static async Task<IResult> Posicao(Guid? lojaId, string? busca, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        var filtro = await LojasDoFiltroAsync(lojas, lojaId, ct);
        if (filtro is null) return Results.NotFound();
        var ids = filtro.Select(l => l.Id).ToList();
        var saldos = await db.Saldos.AsNoTracking().Where(s => ids.Contains(s.LojaId) && s.Quantidade != 0).ToListAsync(ct);
        var skus = (await catalogo.SkusAsync(saldos.Select(s => s.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        var custo = Permissoes.PodeVerCusto(empresa.Perfil);

        var itens = saldos.Where(s => skus.ContainsKey(s.SkuId) && Corresponde(skus[s.SkuId], busca)).GroupBy(s => s.ProdutoId).Select(g =>
        {
            var sku = skus[g.First().SkuId];
            return new ItemDaPosicaoDto(g.Key, sku.Referencia, sku.Descricao, g.Sum(s => s.Quantidade),
                custo ? g.Sum(s => s.ValorEmCusto) : null, g.Sum(s => s.Quantidade * skus[s.SkuId].PrecoVenda));
        }).OrderBy(i => i.Descricao, StringComparer.CurrentCulture).ThenBy(i => i.Referencia, StringComparer.Ordinal).ToList();

        return Results.Ok(new PosicaoDto(itens, itens.Sum(i => i.Quantidade), custo ? itens.Sum(i => i.ValorEmCusto) : null, itens.Sum(i => i.ValorEmVenda)));
    }

    private static decimal? CustoMedioDe(List<SaldoDeEstoque> saldos)
    {
        if (saldos.Count == 0) return null;
        var quantidade = saldos.Sum(s => s.Quantidade);
        return quantidade > 0 ? Math.Round(saldos.Sum(s => s.Quantidade * s.CustoMedio) / quantidade, 4) : saldos.Max(s => s.CustoMedio);
    }

    private static async Task<IResult> Matriz(Guid produtoId, Guid? lojaId, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        var produto = await catalogo.ProdutoAsync(produtoId, ct);
        if (produto is null) return Results.NotFound();
        var visiveis = await lojas.VisiveisAsync(ct);
        if (lojaId is not null && visiveis.All(l => l.Id != lojaId)) return Results.NotFound();
        var idsVisiveis = visiveis.Select(l => l.Id).ToList();
        var saldos = await db.Saldos.AsNoTracking().Where(s => s.ProdutoId == produtoId && idsVisiveis.Contains(s.LojaId)).ToListAsync(ct);
        var doFiltro = saldos.Where(s => lojaId is null || s.LojaId == lojaId).ToLookup(s => s.SkuId);
        var custo = Permissoes.PodeVerCusto(empresa.Perfil);

        var celulas = produto.Skus.Select(sku => new CelulaDto(sku.Id, sku.CorId, sku.Tamanho, sku.CodigoDeBarras, sku.Ativo,
            doFiltro[sku.Id].Sum(s => s.Quantidade), custo ? CustoMedioDe(doFiltro[sku.Id].ToList()) : null, sku.PrecoVenda)).ToList();
        var porLoja = saldos.GroupBy(s => s.LojaId).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantidade));
        var lojasDoProduto = visiveis.Where(l => l.Ativa || porLoja.GetValueOrDefault(l.Id) != 0)
            .Select(l => new LojaDoProdutoDto(l.Id, l.Nome, porLoja.GetValueOrDefault(l.Id))).ToList();

        return Results.Ok(new MatrizDto(produto.Id, produto.Referencia, produto.Descricao, produto.PrecoVenda, custo ? produto.PrecoCusto : null,
            produto.Tamanhos, produto.Cores, celulas, lojasDoProduto, celulas.Sum(c => c.Quantidade)));
    }

    private static async Task<IResult> Kardex(Guid? skuId, Guid? produtoId, Guid? lojaId, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (!Permissoes.PodeVerCusto(empresa.Perfil)) return Permissoes.Negado("Seu perfil não consulta o kardex.");
        if (skuId is null && produtoId is null) return Permissoes.Problema(StatusCodes.Status400BadRequest, "Escolha o produto ou o SKU.");
        var filtro = await LojasDoFiltroAsync(lojas, lojaId, ct);
        if (filtro is null) return Results.NotFound();
        var nomes = (await lojas.VisiveisAsync(ct)).ToDictionary(l => l.Id, l => l.Nome);
        var ids = filtro.Select(l => l.Id).ToList();

        var consulta = db.Movimentos.AsNoTracking().Where(m => ids.Contains(m.LojaId));
        consulta = skuId is not null ? consulta.Where(m => m.SkuId == skuId) : consulta.Where(m => m.ProdutoId == produtoId);
        var movimentos = await consulta.OrderByDescending(m => m.OcorridoEm).ThenByDescending(m => m.Sequencia).Take(LimiteDoKardex).ToListAsync(ct);
        var skus = (await catalogo.SkusAsync(movimentos.Select(m => m.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);

        return Results.Ok(movimentos.Select(m =>
        {
            var sku = skus.GetValueOrDefault(m.SkuId);
            return new MovimentoDto(m.Id, m.OcorridoEm, m.Tipo.ToString(), m.LojaId, nomes.GetValueOrDefault(m.LojaId, "?"),
                m.LojaRelacionadaId is { } r ? nomes.GetValueOrDefault(r, "outra empresa") : null, sku?.Referencia ?? "?", sku?.Cor ?? "?", sku?.Tamanho ?? "?",
                m.Quantidade, m.CustoUnitario, m.SaldoApos, m.CustoMedioApos, m.Motivo, m.Documento, m.Observacao, m.UsuarioNome);
        }).ToList());
    }
}
