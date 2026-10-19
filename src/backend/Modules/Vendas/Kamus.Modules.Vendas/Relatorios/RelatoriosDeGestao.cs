using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Relatorios;

/// <summary>Uma linha por SKU vendido (com o desconto da venda já rateado), base dos relatórios de gestão de vendas.</summary>
internal sealed record ItemVendido(Guid VendaId, DateOnly Data, string Vendedor, SkuDoCatalogo? Sku, string Descricao, Guid ProdutoId, int Pecas, decimal Total, decimal Custo);

internal static class VendasDetalhadas
{
    public static readonly ParametroDoRelatorio Periodo = new("periodo", "Período", TipoDeParametro.Periodo, "mes");

    public static async Task<List<ItemVendido>> CarregarAsync(VendasDbContext db, ICatalogoDeProdutos catalogo, IReadOnlyCollection<Guid> empresaIds, DateOnly de,
        DateOnly ate, CancellationToken ct)
    {
        var vendas = await db.Vendas.AsNoTracking().Include(v => v.Itens)
            .Where(v => empresaIds.Contains(v.EmpresaId) && v.Status == StatusDaVenda.Concluida && v.Data >= de && v.Data <= ate).ToListAsync(ct);
        var skus = (await catalogo.SkusAsync(vendas.SelectMany(v => v.Itens.Select(i => i.SkuId)).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        return vendas.SelectMany(v =>
        {
            var pagos = v.ValoresPagosPorSku();
            return v.Itens.GroupBy(i => i.SkuId).Select(g => new ItemVendido(v.Id, v.Data, v.VendedorNome ?? "Sem vendedor", skus.GetValueOrDefault(g.Key),
                g.First().Descricao, g.First().ProdutoId, pagos[g.Key].Quantidade, pagos[g.Key].ValorPago, g.Sum(i => i.CustoUnitario * i.Quantidade)));
        }).ToList();
    }

    public static decimal Percentual(decimal parte, decimal total) => total == 0 ? 0 : Math.Round(parte / total * 100, 1);
}

/// <summary>Vendas agrupadas por produto, SKU, cor, tamanho, vendedor ou dia, com custo e lucro bruto.</summary>
internal sealed class RelatorioDeVendasAgrupadas(VendasDbContext db, IEmpresaAtiva empresa, ICatalogoDeProdutos catalogo, TimeProvider relogio) : IRelatorio
{
    private static readonly IReadOnlyList<OpcaoDoParametro> Agrupamentos =
    [
        new("produto", "Produto"), new("sku", "Produto, cor e tamanho"), new("cor", "Cor"), new("tamanho", "Tamanho"), new("vendedor", "Vendedor"), new("dia", "Dia"),
    ];

    public string Codigo => "vendas-agrupadas";
    public string Titulo => "Vendas agrupadas";
    public IReadOnlyCollection<string> Perfis => Permissoes.Consultar;
    public string Area => "Vendas";
    public string? Descricao => "Peças, total, custo e lucro bruto do período por produto, cor, tamanho, vendedor ou dia.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [VendasDetalhadas.Periodo, new("agrupamento", "Agrupar por", TipoDeParametro.Opcao, "produto", Agrupamentos)];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, relogio);
        var agrupamento = Agrupamentos.FirstOrDefault(a => a.Valor == parametros.GetValueOrDefault("agrupamento")) ?? Agrupamentos[0];
        var itens = await VendasDetalhadas.CarregarAsync(db, catalogo, empresa.EmpresaIds, de, ate, ct);
        var total = itens.Sum(i => i.Total);

        string Chave(ItemVendido i) => agrupamento.Valor switch
        {
            "sku" => i.Descricao,
            "cor" => i.Sku?.Cor ?? "—",
            "tamanho" => i.Sku?.Tamanho ?? "—",
            "vendedor" => i.Vendedor,
            "dia" => i.Data.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => i.Sku is { } s ? $"{s.Referencia} · {s.Descricao}" : i.Descricao,
        };

        var grupos = itens.GroupBy(Chave).Select(g => (Chave: g.Key, Data: g.First().Data, Vendas: g.Select(i => i.VendaId).Distinct().Count(), Pecas: g.Sum(i => i.Pecas),
            Total: g.Sum(i => i.Total), Custo: g.Sum(i => i.Custo)));
        grupos = agrupamento.Valor == "dia"
            ? grupos.OrderBy(g => g.Data)
            : grupos.OrderByDescending(g => g.Total).ThenBy(g => g.Chave, StringComparer.CurrentCulture);

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio(agrupamento.Valor == "sku" ? "SKU" : agrupamento.Rotulo, agrupamento.Valor == "dia" ? TipoColuna.Data : TipoColuna.Texto, Largura: 2f),
            new ColunaRelatorio("Vendas", TipoColuna.Inteiro, Largura: 0.6f),
            new ColunaRelatorio("Peças", TipoColuna.Inteiro, Totalizar: true, Largura: 0.6f),
            new ColunaRelatorio("Total", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("% do total", TipoColuna.Numero, Largura: 0.7f),
            new ColunaRelatorio("Custo", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Lucro bruto", TipoColuna.Moeda, Totalizar: true),
        ], grupos.Select(g => (IReadOnlyList<object?>)
            [agrupamento.Valor == "dia" ? g.Data : g.Chave, g.Vendas, g.Pecas, g.Total, VendasDetalhadas.Percentual(g.Total, total), g.Custo, g.Total - g.Custo]).ToList(),
        [new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"), new("Agrupamento", agrupamento.Rotulo)]);
    }
}

/// <summary>Curva ABC de produtos pelo valor vendido: A = até 80% do faturamento, B = até 95%, C = o resto.</summary>
internal sealed class RelatorioCurvaAbc(VendasDbContext db, IEmpresaAtiva empresa, ICatalogoDeProdutos catalogo, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "curva-abc";
    public string Titulo => "Curva ABC de produtos";
    public IReadOnlyCollection<string> Perfis => Permissoes.Consultar;
    public string Area => "Gestão";
    public string? Descricao => "Quais produtos fazem o faturamento: A somam 80% das vendas, B os 15% seguintes e C o restante.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros => [VendasDetalhadas.Periodo];

    public static string Classe(decimal acumuladoAntes) => acumuladoAntes < 80 ? "A" : acumuladoAntes < 95 ? "B" : "C";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, relogio);
        var itens = await VendasDetalhadas.CarregarAsync(db, catalogo, empresa.EmpresaIds, de, ate, ct);
        var total = itens.Sum(i => i.Total);
        var produtos = itens.GroupBy(i => i.ProdutoId)
            .Select(g => (Referencia: g.First().Sku?.Referencia ?? "", Descricao: g.First().Sku?.Descricao ?? g.First().Descricao, Pecas: g.Sum(i => i.Pecas), Total: g.Sum(i => i.Total)))
            .OrderByDescending(p => p.Total).ThenBy(p => p.Referencia, StringComparer.Ordinal).ToList();

        var linhas = new List<IReadOnlyList<object?>>();
        var acumulado = 0m;
        foreach (var p in produtos)
        {
            var antes = total == 0 ? 0 : acumulado / total * 100;
            acumulado += p.Total;
            linhas.Add([p.Referencia, p.Descricao, p.Pecas, p.Total, VendasDetalhadas.Percentual(p.Total, total), VendasDetalhadas.Percentual(acumulado, total), Classe(antes)]);
        }

        var porClasse = linhas.GroupBy(l => (string)l[^1]!).ToDictionary(g => g.Key, g => g.Count());
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Referência", Largura: 0.8f),
            new ColunaRelatorio("Produto", Largura: 2f),
            new ColunaRelatorio("Peças", TipoColuna.Inteiro, Totalizar: true, Largura: 0.6f),
            new ColunaRelatorio("Total", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("% do total", TipoColuna.Numero, Largura: 0.7f),
            new ColunaRelatorio("% acumulado", TipoColuna.Numero, Largura: 0.7f),
            new ColunaRelatorio("Classe", Largura: 0.5f),
        ], linhas,
        [
            new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"),
            new("Resumo", $"A: {porClasse.GetValueOrDefault("A")} · B: {porClasse.GetValueOrDefault("B")} · C: {porClasse.GetValueOrDefault("C")} produtos"),
        ]);
    }
}
