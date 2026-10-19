using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

/// <summary>Números de vendas e condicionais para o dashboard e o resumo diário (calculados em memória a partir das vendas do período).</summary>
internal sealed class IndicadoresDeVendas(VendasDbContext db) : IIndicadoresDeVendas
{
    private const int QuantidadeDeMaisVendidos = 5;

    public async Task<ResumoDeVendas> VendasAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly de, DateOnly ate, Guid? registradasPor = null, CancellationToken ct = default)
    {
        var q = db.Vendas.AsNoTracking().Include(v => v.Itens).Include(v => v.Pagamentos)
            .Where(v => empresaIds.Contains(v.EmpresaId) && v.Status == StatusDaVenda.Concluida && v.Data >= de && v.Data <= ate);
        if (registradasPor is { } usuario) q = q.Where(v => v.RegistradaPorId == usuario);
        var vendas = await q.ToListAsync(ct);

        var porData = vendas.ToLookup(v => v.Data);
        var dias = new List<VendasDoDia>();
        for (var d = de; d <= ate; d = d.AddDays(1))
            dias.Add(new VendasDoDia(d, porData[d].Count(), porData[d].Sum(v => v.Pecas), porData[d].Sum(v => v.Total)));

        var total = vendas.Sum(v => v.Total);
        var porForma = vendas.SelectMany(v => v.Pagamentos).GroupBy(p => p.FormaNome)
            .Select(g => new TotalPorNome(g.Key, g.Select(p => p.VendaId).Distinct().Count(), g.Sum(p => p.Valor)))
            .OrderByDescending(t => t.Total).ToList();
        var porVendedor = vendas.GroupBy(v => v.VendedorNome ?? "Sem vendedor")
            .Select(g => new TotalPorNome(g.Key, g.Count(), g.Sum(v => v.Total)))
            .OrderByDescending(t => t.Total).ToList();
        var maisVendidos = vendas.SelectMany(v => v.Itens).GroupBy(i => i.SkuId)
            .Select(g => new MaisVendido(g.Key, g.First().Descricao, g.Sum(i => i.Quantidade), g.Sum(i => i.Total)))
            .OrderByDescending(m => m.Pecas).ThenByDescending(m => m.Total).Take(QuantidadeDeMaisVendidos).ToList();

        return new ResumoDeVendas(vendas.Count, vendas.Sum(v => v.Pecas), total, vendas.Count == 0 ? 0 : Dinheiro.Arredondar(total / vendas.Count),
            dias, porForma, porVendedor, maisVendidos);
    }

    public async Task<CondicionaisNaRua> CondicionaisAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly hoje, CancellationToken ct = default)
    {
        var abertas = await db.Condicionais.AsNoTracking().Include(c => c.Itens).Where(c => empresaIds.Contains(c.EmpresaId) && !c.Fechada).ToListAsync(ct);
        return new CondicionaisNaRua(abertas.Count, abertas.Count(c => c.Situacao(hoje) == SituacaoDaCondicional.Atrasada), abertas.Sum(c => c.Pecas), abertas.Sum(c => c.Valor));
    }
}
