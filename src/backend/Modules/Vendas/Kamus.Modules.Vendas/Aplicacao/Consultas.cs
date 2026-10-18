using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

/// <param name="Status">concluidas, canceladas ou todas.</param>
public sealed record FiltroDeVendas(DateOnly? De, DateOnly? Ate, Guid? VendedorId, string Status, string? Busca, Guid? ClienteId = null);

public sealed record LinhaDeVenda(Guid Id, Guid EmpresaId, int Numero, DateOnly Data, string? ClienteNome, Guid? VendedorId, string? VendedorNome, int Pecas,
    decimal Total, string Formas, string Status, string? NotaExterna);

/// <summary>Totais só das vendas concluídas.</summary>
public sealed record ListaDeVendas(IReadOnlyList<LinhaDeVenda> Itens, int Quantidade, int Pecas, decimal Total, decimal TicketMedio);

/// <param name="Total">Total das vendas mais a diferença das trocas (base da comissão).</param>
/// <param name="ComissaoPendente">Parte ainda não incluída em fechamento.</param>
public sealed record ComissaoDoVendedor(Guid? VendedorId, string Vendedor, int Vendas, decimal Total, decimal Comissao, decimal ComissaoPendente);

/// <summary>Consultas compartilhadas pela tela e pelos relatórios.</summary>
internal static class Consultas
{
    public static readonly string[] Status = ["concluidas", "canceladas", "todas"];

    /// <summary>Vendas visíveis para o usuário: o perfil Vendedor só vê as que registrou.</summary>
    public static IQueryable<Venda> Visiveis(VendasDbContext db, IEmpresaAtiva empresa, Guid? usuarioId)
    {
        var ids = empresa.EmpresaIds;
        var vendas = db.Vendas.AsNoTracking().Include(v => v.Itens).Include(v => v.Pagamentos).Where(v => ids.Contains(v.EmpresaId));
        return Permissoes.VeTudo(empresa) ? vendas : vendas.Where(v => v.RegistradaPorId == usuarioId);
    }

    public static async Task<List<Venda>> FiltrarAsync(VendasDbContext db, IEmpresaAtiva empresa, Guid? usuarioId, FiltroDeVendas f, CancellationToken ct)
    {
        var q = Visiveis(db, empresa, usuarioId);
        if (f.De is { } de) q = q.Where(v => v.Data >= de);
        if (f.Ate is { } ate) q = q.Where(v => v.Data <= ate);
        if (f.VendedorId is { } vendedor) q = q.Where(v => v.VendedorId == vendedor);
        if (f.ClienteId is { } cliente) q = q.Where(v => v.ClienteId == cliente);
        if (f.Status == "concluidas") q = q.Where(v => v.Status == StatusDaVenda.Concluida);
        if (f.Status == "canceladas") q = q.Where(v => v.Status == StatusDaVenda.Cancelada);
        var vendas = await q.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(f.Busca))
        {
            var termo = TextoParaBusca.Normalizar(f.Busca);
            vendas = vendas.Where(v => v.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture) == f.Busca.Trim()
                || TextoParaBusca.Normalizar(v.ClienteNome).Contains(termo, StringComparison.Ordinal)
                || TextoParaBusca.Normalizar(v.NotaExterna).Contains(termo, StringComparison.Ordinal)).ToList();
        }
        return vendas.OrderByDescending(v => v.Data).ThenByDescending(v => v.Numero).ToList();
    }

    public static string Formas(Venda v) => string.Join(" + ", v.Pagamentos.Select(p => p.Parcelas > 1 ? $"{p.FormaNome} {p.Parcelas}x" : p.FormaNome));

    public static async Task<ListaDeVendas> ListarAsync(VendasDbContext db, IEmpresaAtiva empresa, Guid? usuarioId, FiltroDeVendas f, CancellationToken ct)
    {
        var vendas = await FiltrarAsync(db, empresa, usuarioId, f, ct);
        var concluidas = vendas.Where(v => v.Status == StatusDaVenda.Concluida).ToList();
        var total = concluidas.Sum(v => v.Total);
        return new ListaDeVendas(
            vendas.Select(v => new LinhaDeVenda(v.Id, v.EmpresaId, v.Numero, v.Data, v.ClienteNome, v.VendedorId, v.VendedorNome, v.Pecas, v.Total, Formas(v),
                v.Status.ToString(), v.NotaExterna)).ToList(),
            concluidas.Count, concluidas.Sum(v => v.Pecas), total, concluidas.Count == 0 ? 0 : Math.Round(total / concluidas.Count, 2));
    }

    /// <summary>Comissão por vendedor sobre o total das vendas concluídas no período, ajustada pelas trocas (data da troca).</summary>
    public static async Task<List<ComissaoDoVendedor>> ComissoesAsync(VendasDbContext db, IEmpresaAtiva empresa, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var vendas = await FiltrarAsync(db, empresa, null, new FiltroDeVendas(de, ate, null, "concluidas", null), ct);
        var trocas = await TrocasDoPeriodoAsync(db, empresa.EmpresaIds, de, ate, ct);
        var nomes = vendas.Where(v => v.VendedorId is not null).OrderBy(v => v.Numero).GroupBy(v => v.VendedorId!.Value).ToDictionary(g => g.Key, g => g.Last().VendedorNome);
        foreach (var t in trocas.Where(t => t.VendedorId is not null)) nomes.TryAdd(t.VendedorId!.Value, t.VendedorNome);
        return vendas.Select(v => v.VendedorId).Concat(trocas.Select(t => t.VendedorId)).Distinct()
            .Select(id =>
            {
                var dele = vendas.Where(v => v.VendedorId == id).ToList();
                var trocasDele = trocas.Where(t => t.VendedorId == id).ToList();
                return new ComissaoDoVendedor(id, id is null ? "Sem vendedor" : nomes.GetValueOrDefault(id.Value) ?? "?", dele.Count,
                    dele.Sum(v => v.Total) + trocasDele.Sum(t => t.Diferenca),
                    dele.Sum(v => v.Comissao) + trocasDele.Sum(t => t.Comissao),
                    dele.Where(v => v.FechamentoId is null).Sum(v => v.Comissao) + trocasDele.Where(t => t.FechamentoId is null).Sum(t => t.Comissao));
            })
            .OrderBy(c => c.VendedorId is null).ThenBy(c => c.Vendedor, StringComparer.CurrentCulture).ToList();
    }

    public static Task<List<Troca>> TrocasDoPeriodoAsync(VendasDbContext db, IReadOnlyList<Guid> empresaIds, DateOnly de, DateOnly ate, CancellationToken ct) =>
        db.Trocas.Include(t => t.Devolvidos).Include(t => t.Levados).Include(t => t.Pagamentos)
            .Where(t => empresaIds.Contains(t.EmpresaId) && t.Data >= de && t.Data <= ate).ToListAsync(ct);
}
