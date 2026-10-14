using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Aplicacao;

internal static class Saldos
{
    /// <summary>Saldos da loja para os SKUs pedidos; os que ainda não existem são criados (zerados) e adicionados ao contexto.</summary>
    public static async Task<Dictionary<Guid, SaldoDeEstoque>> DaLojaAsync(EstoqueDbContext db, Guid lojaId, IEnumerable<SkuDoCatalogo> skus, CancellationToken ct)
    {
        var lista = skus.ToList();
        var ids = lista.Select(s => s.Id).ToList();
        var existentes = await db.Saldos.Where(s => s.LojaId == lojaId && ids.Contains(s.SkuId)).ToDictionaryAsync(s => s.SkuId, ct);
        foreach (var sku in lista.Where(s => !existentes.ContainsKey(s.Id)))
        {
            var novo = SaldoDeEstoque.Novo(lojaId, sku.Id, sku.ProdutoId);
            db.Saldos.Add(novo);
            existentes[sku.Id] = novo;
        }
        return existentes;
    }

    public static string Nome(SkuDoCatalogo sku) => $"{sku.Referencia} {sku.Cor} {sku.Tamanho}";
}
