using System.Globalization;
using Kamus.Modules.Indicadores.Aplicacao;
using Kamus.SharedKernel.Indicadores;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Kamus.SharedKernel.Tempo;

namespace Kamus.Modules.Indicadores.Relatorios;

internal static class Filtros
{
    public static (DateOnly De, DateOnly Ate) Periodo(IReadOnlyDictionary<string, string?> p, DateOnly hoje)
    {
        static DateOnly? Data(string? texto) => DateOnly.TryParse(texto, CultureInfo.InvariantCulture, out var d) ? d : null;
        return (Data(p.GetValueOrDefault("de")) ?? new DateOnly(hoje.Year, hoje.Month, 1), Data(p.GetValueOrDefault("ate")) ?? hoje);
    }
}

/// <summary>Produtos com estoque e sem venda há N dias (ou nunca vendidos): dinheiro parado na prateleira.</summary>
internal sealed class RelatorioDeProdutosSemGiro(IIndicadoresDeEstoque estoque, IIndicadoresDeVendas vendas, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public const int DiasPadrao = 60;

    public string Codigo => "produtos-sem-giro";
    public string Titulo => "Produtos sem giro";
    public IReadOnlyCollection<string> Perfis => Aplicacao.Perfis.Estoque;
    public string Area => "Estoque";
    public string? Descricao => "Produtos com estoque que não vendem há muitos dias (ou nunca venderam), com o valor parado a custo.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros => [new("dias", "Sem venda há pelo menos (dias)", TipoDeParametro.Inteiro, DiasPadrao.ToString(CultureInfo.InvariantCulture))];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var dias = int.TryParse(parametros.GetValueOrDefault("dias"), out var d) && d >= 0 ? d : DiasPadrao;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var limite = hoje.AddDays(-dias);
        var produtos = await estoque.PorProdutoAsync(empresa.EmpresaIds, ct);
        var ultimas = await vendas.UltimaVendaPorProdutoAsync(empresa.EmpresaIds, ct);

        var parados = produtos
            .Select(p => (Produto: p, Ultima: ultimas.TryGetValue(p.ProdutoId, out var u) ? u : (DateOnly?)null))
            .Where(x => x.Ultima is null || x.Ultima < limite)
            .OrderByDescending(x => x.Produto.ValorDeCusto).ThenBy(x => x.Produto.Referencia, StringComparer.Ordinal)
            .ToList();

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Referência", Largura: 0.8f),
            new ColunaRelatorio("Produto", Largura: 2f),
            new ColunaRelatorio("Peças em estoque", TipoColuna.Inteiro, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Valor a custo", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Última venda", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Situação", Largura: 1f),
        ], parados.Select(x => (IReadOnlyList<object?>)
            [x.Produto.Referencia, x.Produto.Descricao, x.Produto.Pecas, x.Produto.ValorDeCusto, x.Ultima,
                x.Ultima is { } u ? $"{hoje.DayNumber - u.DayNumber} dias sem venda" : "Nunca vendido"]).ToList(),
        [new("Sem venda há", $"{dias} dias ou mais"), new("Resumo", $"{parados.Count} produtos · {Painel.Moeda(parados.Sum(x => x.Produto.ValorDeCusto))} parados a custo")]);
    }
}

/// <summary>
/// DRE gerencial (regime de competência): receita das vendas, taxas de cartão, custo das mercadorias vendidas, outras receitas e despesas por categoria.
/// Títulos gerados por vendas, trocas e notas de entrada e o grupo "Mercadorias" ficam de fora (já entram como receita ou viram estoque/custo).
/// </summary>
internal sealed class RelatorioDre(IIndicadoresDeVendas vendas, IIndicadoresFinanceiros financeiro, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    private static readonly string[] OrigensForaDaDre = ["Venda", "Troca", "NotaDeEntrada"];
    private const string GrupoDeMercadorias = "Mercadorias";

    public string Codigo => "dre";
    public string Titulo => "DRE gerencial";
    public IReadOnlyCollection<string> Perfis => Aplicacao.Perfis.Financeiro;
    public string Area => "Gestão";
    public string? Descricao => "Resultado do período: vendas, taxas, custo das mercadorias, outras receitas e despesas por categoria (por competência).";
    public IReadOnlyList<ParametroDoRelatorio> Parametros => [new("periodo", "Período", TipoDeParametro.Periodo, "mes")];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, CalendarioBrasilia.Hoje(relogio));
        var resultado = await vendas.ResultadoAsync(empresa.EmpresaIds, de, ate, ct);
        var lancamentos = (await financeiro.PorCategoriaAsync(empresa.EmpresaIds, de, ate, ct))
            .Where(l => !OrigensForaDaDre.Contains(l.Origem) && l.Grupo != GrupoDeMercadorias).ToList();

        var receita = resultado.ReceitaBruta;
        var linhas = new List<IReadOnlyList<object?>>();
        void Linha(string descricao, decimal valor) => linhas.Add([descricao, valor, receita == 0 ? 0m : Math.Round(valor / receita * 100, 1)]);

        var liquida = receita - resultado.TaxasDeCartao;
        var lucroBruto = liquida - resultado.CustoDasMercadorias;
        Linha("Receita bruta de vendas", receita);
        Linha("(−) Taxas de cartão", -resultado.TaxasDeCartao);
        Linha("(=) Receita líquida", liquida);
        Linha("(−) Custo das mercadorias vendidas", -resultado.CustoDasMercadorias);
        Linha("(=) Lucro bruto", lucroBruto);

        var outras = lancamentos.Where(l => l.Tipo == "Receita").GroupBy(l => l.Categoria).Select(g => (Nome: g.Key, Valor: g.Sum(l => l.Valor)))
            .OrderBy(x => x.Nome, StringComparer.CurrentCulture).ToList();
        foreach (var o in outras) Linha($"Outras receitas · {o.Nome}", o.Valor);
        var despesas = lancamentos.Where(l => l.Tipo == "Despesa").GroupBy(l => (l.Grupo, l.Categoria)).Select(g => (g.Key.Grupo, g.Key.Categoria, Valor: g.Sum(l => l.Valor)))
            .OrderBy(x => x.Grupo, StringComparer.CurrentCulture).ThenBy(x => x.Categoria, StringComparer.CurrentCulture).ToList();
        foreach (var d in despesas) Linha(d.Grupo == d.Categoria ? d.Categoria : $"{d.Grupo} · {d.Categoria}", -d.Valor);
        Linha("(=) Resultado do período", lucroBruto + outras.Sum(o => o.Valor) - despesas.Sum(d => d.Valor));

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Descrição", Largura: 3f),
            new ColunaRelatorio("Valor", TipoColuna.Moeda),
            new ColunaRelatorio("% da receita", TipoColuna.Numero, Largura: 0.8f),
        ], linhas,
        [new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"), new("Vendas", $"{resultado.Vendas} vendas concluídas"), new("Regime", "Competência")]);
    }
}
