using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Aplicacao;

internal sealed class CatalogoDeProdutos(CadastrosDbContext db) : ICatalogoDeProdutos
{
    public async Task<ProdutoDoCatalogo?> ProdutoAsync(Guid produtoId, CancellationToken ct = default)
    {
        var produto = await db.Produtos.AsNoTracking().Include(p => p.Skus).SingleOrDefaultAsync(p => p.Id == produtoId, ct);
        if (produto is null) return null;
        var grade = await db.Grades.AsNoTracking().SingleAsync(g => g.Id == produto.GradeId, ct);
        var corIds = produto.Skus.Select(s => s.CorId).Distinct().ToList();
        var cores = (await db.Cores.AsNoTracking().Where(c => corIds.Contains(c.Id)).ToListAsync(ct)).OrderBy(c => corIds.IndexOf(c.Id)).ToList();
        var porId = cores.ToDictionary(c => c.Id);
        return new ProdutoDoCatalogo(produto.Id, produto.Referencia, produto.Descricao, produto.Marca, produto.Categoria, produto.PrecoVenda,
            produto.PrecoCusto, produto.Ativo, grade.Tamanhos, cores.Select(c => new CorDoCatalogo(c.Id, c.Nome, c.Hex)).ToList(),
            produto.Skus.OrderBy(s => corIds.IndexOf(s.CorId)).ThenBy(s => s.OrdemTamanho).Select(s => Sku(produto, s, porId[s.CorId])).ToList());
    }

    public async Task<IReadOnlyList<SkuDoCatalogo>> SkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default)
    {
        if (skuIds.Count == 0) return [];
        var produtoIds = await db.Skus.Where(s => skuIds.Contains(s.Id)).Select(s => s.ProdutoId).Distinct().ToListAsync(ct);
        return (await MontarAsync(db.Produtos.Where(p => produtoIds.Contains(p.Id)), ct)).Where(s => skuIds.Contains(s.Id)).ToList();
    }

    public Task<IReadOnlyList<SkuDoCatalogo>> TodosOsSkusAsync(CancellationToken ct = default) => MontarAsync(db.Produtos, ct);

    private async Task<IReadOnlyList<SkuDoCatalogo>> MontarAsync(IQueryable<Produto> consulta, CancellationToken ct)
    {
        var produtos = await consulta.AsNoTracking().Include(p => p.Skus).ToListAsync(ct);
        var cores = await db.Cores.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        return produtos.SelectMany(p => p.Skus.Select(s => Sku(p, s, cores[s.CorId]))).ToList();
    }

    private static SkuDoCatalogo Sku(Produto p, Sku s, Cor cor) => new(s.Id, p.Id, p.Referencia, p.Descricao, s.CorId, cor.Nome, cor.Hex,
        s.Tamanho, s.OrdemTamanho, s.CodigoDeBarras, p.PrecoDe(s), p.PrecoCusto, s.Ativo && p.Ativo);
}
