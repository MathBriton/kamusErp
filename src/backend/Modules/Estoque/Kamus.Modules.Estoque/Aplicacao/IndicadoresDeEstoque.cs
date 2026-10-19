using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Aplicacao;

/// <summary>
/// Valor do estoque e quebras de grade (somando as lojas das empresas) para o dashboard e o resumo diário.
/// Quebra de grade: produto com saldo em parte da grade e algum SKU (ativo) que já teve estoque e zerou.
/// </summary>
internal sealed class IndicadoresDeEstoque(EstoqueDbContext db, ICatalogoDeProdutos catalogo) : IIndicadoresDeEstoque
{
    private const int QuantidadeDeQuebras = 5;

    public async Task<ResumoDeEstoque> ResumoAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default)
    {
        var lojas = db.Lojas.Where(l => empresaIds.Contains(l.EmpresaId)).Select(l => l.Id);
        var saldos = await db.Saldos.AsNoTracking().Where(s => lojas.Contains(s.LojaId)).ToListAsync(ct);
        var porSku = saldos.GroupBy(s => s.SkuId)
            .Select(g => (SkuId: g.Key, g.First().ProdutoId, Quantidade: g.Sum(s => s.Quantidade), Custo: g.Where(s => s.Quantidade > 0).Sum(s => s.Quantidade * s.CustoMedio)))
            .ToList();
        var skus = (await catalogo.SkusAsync(porSku.Select(s => s.SkuId).ToList(), ct)).ToDictionary(s => s.Id);

        var positivos = porSku.Where(s => s.Quantidade > 0).ToList();
        var quebras = porSku.Where(s => skus.TryGetValue(s.SkuId, out var sku) && sku.Ativo)
            .GroupBy(s => s.ProdutoId)
            .Where(g => g.Any(s => s.Quantidade > 0) && g.Any(s => s.Quantidade <= 0))
            .Select(g => new QuebraDeGrade(g.Key, skus[g.First().SkuId].Descricao,
                g.Where(s => s.Quantidade <= 0).Select(s => skus[s.SkuId]).OrderBy(s => s.Cor, StringComparer.CurrentCulture).ThenBy(s => s.OrdemTamanho)
                    .Select(s => $"{s.Cor} {s.Tamanho}").ToList()))
            .OrderByDescending(q => q.Esgotados.Count).ThenBy(q => q.Descricao, StringComparer.CurrentCulture)
            .ToList();

        return new ResumoDeEstoque(
            positivos.Sum(s => s.Quantidade),
            Math.Round(positivos.Sum(s => s.Custo), 2),
            positivos.Sum(s => s.Quantidade * (skus.TryGetValue(s.SkuId, out var sku) ? sku.PrecoVenda : 0)),
            porSku.Count(s => s.Quantidade < 0),
            quebras.Take(QuantidadeDeQuebras).ToList(),
            quebras.Count);
    }

    public async Task<IReadOnlyList<EstoqueDoProduto>> PorProdutoAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default)
    {
        var lojas = db.Lojas.Where(l => empresaIds.Contains(l.EmpresaId)).Select(l => l.Id);
        var saldos = await db.Saldos.AsNoTracking().Where(s => lojas.Contains(s.LojaId) && s.Quantidade > 0).ToListAsync(ct);
        var skus = (await catalogo.SkusAsync(saldos.Select(s => s.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        return saldos.GroupBy(s => s.ProdutoId)
            .Select(g =>
            {
                var sku = g.Select(s => skus.GetValueOrDefault(s.SkuId)).FirstOrDefault(s => s is not null);
                return new EstoqueDoProduto(g.Key, sku?.Referencia ?? "", sku?.Descricao ?? "(produto removido)", g.Sum(s => s.Quantidade),
                    Math.Round(g.Sum(s => s.Quantidade * s.CustoMedio), 2));
            })
            .OrderBy(p => p.Descricao, StringComparer.CurrentCulture).ToList();
    }
}
