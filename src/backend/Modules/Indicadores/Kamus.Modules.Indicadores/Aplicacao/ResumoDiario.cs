using Kamus.Modules.Indicadores.Dominio;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Indicadores;
using static Kamus.Modules.Indicadores.Aplicacao.Painel;

namespace Kamus.Modules.Indicadores.Aplicacao;

/// <param name="TemMovimento">Houve venda ou há algo a cobrar/olhar (sem isso, só envia com "enviar mesmo sem movimento").</param>
public sealed record PreviaDoResumo(string Titulo, IReadOnlyList<string> Linhas, bool TemMovimento)
{
    public const int TamanhoMaximo = 1000;

    public string Mensagem()
    {
        var texto = string.Join('\n', Linhas);
        return texto.Length <= TamanhoMaximo ? texto : string.Concat(texto.AsSpan(0, TamanhoMaximo - 1), "…");
    }
}

/// <summary>
/// Texto do resumo diário de um usuário: cada bloco usa só as empresas em que o perfil dele vê aquele bloco
/// (o Vendedor vê as vendas que ele registrou) e só entra se estiver ligado nas preferências.
/// </summary>
public sealed class ResumoDiario(IIndicadoresDeVendas vendas, IIndicadoresFinanceiros financeiro, IIndicadoresDeEstoque estoque)
{
    public async Task<PreviaDoResumo> MontarAsync(Guid usuarioId, IReadOnlyCollection<AcessoDoUsuario> acessos, PreferenciasDoResumo p, DateOnly hoje, CancellationToken ct)
    {
        IReadOnlyCollection<Guid> Empresas(string[] perfis) => acessos.Where(a => perfis.Contains(a.Perfil)).Select(a => a.EmpresaId).Distinct().ToList();
        var linhas = new List<string>();
        var movimento = false;

        if (p.Vendas)
        {
            var completas = Empresas(Perfis.VendasCompletas);
            var proprias = Empresas(Perfis.VendasProprias).Except(completas).ToList();
            if (completas.Count > 0 || proprias.Count > 0)
            {
                var hojeR = await SomarAsync(completas, proprias, usuarioId, hoje, hoje, ct);
                var ontem = await SomarAsync(completas, proprias, usuarioId, hoje.AddDays(-1), hoje.AddDays(-1), ct);
                var total = hojeR.Sum(r => r.Total);
                var quantidade = hojeR.Sum(r => r.Vendas);
                if (quantidade == 0)
                    linhas.Add("Vendas: nenhuma venda hoje");
                else
                {
                    movimento = true;
                    var variacao = Variacao(total, ontem.Sum(r => r.Total));
                    linhas.Add($"Vendas: {Moeda(total)} em {Plural(quantidade, "venda", "vendas")} ({Plural(hojeR.Sum(r => r.Pecas), "peça", "peças")})"
                        + $" · ticket médio {Moeda(Math.Round(total / quantidade, 2, MidpointRounding.AwayFromZero))}"
                        + (variacao is { } v ? $" · {(v >= 0 ? "+" : "")}{v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}% em relação a ontem" : ""));
                    var mais = hojeR.SelectMany(r => r.MaisVendidos).GroupBy(m => m.SkuId)
                        .Select(g => (g.First().Descricao, Pecas: g.Sum(m => m.Pecas))).OrderByDescending(m => m.Pecas).Take(3).ToList();
                    linhas.Add("Mais vendidos: " + string.Join(", ", mais.Select(m => $"{m.Descricao} ({m.Pecas})")));
                    var formas = hojeR.SelectMany(r => r.PorForma).GroupBy(f => f.Nome).Select(g => (Nome: g.Key, Total: g.Sum(f => f.Total))).OrderByDescending(f => f.Total);
                    linhas.Add("Formas de pagamento: " + string.Join(" · ", formas.Select(f => $"{f.Nome} {Moeda(f.Total)}")));
                }
            }
        }

        var empresasDoFinanceiro = Empresas(Perfis.Financeiro);
        var verCrediario = p.Crediario && Empresas(Perfis.Crediario).Count > 0;
        if ((p.Financeiro && empresasDoFinanceiro.Count > 0) || verCrediario)
        {
            var f = await financeiro.ResumoAsync(p.Financeiro ? empresasDoFinanceiro : Empresas(Perfis.Crediario), hoje, ct);
            if (p.Financeiro && empresasDoFinanceiro.Count > 0)
            {
                linhas.Add($"Amanhã: a receber {Moeda(f.AReceber.Amanha)} · a pagar {Moeda(f.APagar.Amanha)}");
                if (f.APagar.ParcelasVencidas > 0)
                    linhas.Add($"Contas a pagar vencidas: {f.APagar.ParcelasVencidas} ({Moeda(f.APagar.Vencido)})");
                if (f.AReceber.ParcelasVencidas > 0)
                    linhas.Add($"Contas a receber vencidas: {f.AReceber.ParcelasVencidas} ({Moeda(f.AReceber.Vencido)})");
                movimento |= f.AReceber.Amanha > 0 || f.APagar.Amanha > 0 || f.APagar.ParcelasVencidas > 0 || f.AReceber.ParcelasVencidas > 0;
            }
            if (verCrediario && f.ClientesEmAtraso > 0)
            {
                linhas.Add($"Crediário: {Moeda(f.CrediarioEmAtraso)} em atraso ({Plural(f.ClientesEmAtraso, "cliente", "clientes")})");
                movimento = true;
            }
        }

        var empresasDasCondicionais = Empresas(Perfis.Condicionais);
        if (p.Condicionais && empresasDasCondicionais.Count > 0)
        {
            var c = await vendas.CondicionaisAsync(empresasDasCondicionais, hoje, ct);
            if (c.Abertas > 0)
            {
                linhas.Add($"Condicionais: {Plural(c.Abertas, "aberta", "abertas")} na rua ({Plural(c.Pecas, "peça", "peças")}, {Moeda(c.Valor)})"
                    + (c.Atrasadas > 0 ? $" · {Plural(c.Atrasadas, "condicional atrasada", "condicionais atrasadas")}" : ""));
                movimento |= c.Atrasadas > 0;
            }
        }

        var empresasDoEstoque = Empresas(Perfis.Estoque);
        if (p.Estoque && empresasDoEstoque.Count > 0)
        {
            var e = await estoque.ResumoAsync(empresasDoEstoque, ct);
            if (e.TotalDeQuebras > 0)
                linhas.Add($"Quebra de grade: {Plural(e.TotalDeQuebras, "produto", "produtos")} ("
                    + string.Join("; ", e.Quebras.Take(3).Select(q => $"{q.Descricao}: {string.Join(", ", q.Esgotados)}")) + ")");
            if (e.SkusNegativos > 0)
                linhas.Add($"Estoque negativo: {Plural(e.SkusNegativos, "SKU", "SKUs")}");
            movimento |= e.TotalDeQuebras > 0 || e.SkusNegativos > 0;
        }

        return new PreviaDoResumo($"Resumo do dia {hoje:dd/MM}", linhas, movimento);
    }

    private async Task<List<ResumoDeVendas>> SomarAsync(IReadOnlyCollection<Guid> completas, List<Guid> proprias, Guid usuarioId, DateOnly de, DateOnly ate,
        CancellationToken ct)
    {
        var resumos = new List<ResumoDeVendas>();
        if (completas.Count > 0) resumos.Add(await vendas.VendasAsync(completas, de, ate, null, ct));
        if (proprias.Count > 0) resumos.Add(await vendas.VendasAsync(proprias, de, ate, usuarioId, ct));
        return resumos;
    }
}
