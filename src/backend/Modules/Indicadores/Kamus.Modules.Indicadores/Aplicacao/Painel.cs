using System.Globalization;
using Kamus.SharedKernel.Indicadores;

namespace Kamus.Modules.Indicadores.Aplicacao;

/// <param name="Variacao">Em %, contra o período anterior; nulo quando o anterior foi zero.</param>
public sealed record Cartao(decimal Total, int Vendas, int Pecas, decimal TicketMedio, decimal TotalAnterior, decimal? Variacao);

/// <param name="SomenteAsMinhas">Visão do vendedor: só as vendas que ele registrou.</param>
public sealed record BlocoDeVendas(Cartao Periodo, Cartao Mes, IReadOnlyList<VendasDoDia> Ultimos30Dias, IReadOnlyList<TotalPorNome> PorForma,
    IReadOnlyList<TotalPorNome> PorVendedor, IReadOnlyList<MaisVendido> MaisVendidos, bool SomenteAsMinhas);

/// <param name="Gravidade">perigo ou aviso.</param>
/// <param name="Link">Rota do web com o detalhe.</param>
public sealed record Alerta(string Tipo, string Gravidade, string Titulo, string Detalhe, string Link);

/// <summary>Blocos nulos = o perfil não vê.</summary>
public sealed record Dashboard(string Periodo, DateOnly De, DateOnly Ate, BlocoDeVendas? Vendas, ResumoFinanceiro? Financeiro, ResumoDeEstoque? Estoque,
    CondicionaisNaRua? Condicionais, IReadOnlyList<Alerta> Alertas);

/// <summary>Período escolhido no dashboard e o período anterior do mesmo tamanho, para comparar.</summary>
public sealed record Periodo(string Codigo, DateOnly De, DateOnly Ate, DateOnly AnteriorDe, DateOnly AnteriorAte)
{
    public static readonly string[] Codigos = ["hoje", "7dias", "mes"];

    public static Periodo? Escolher(string? codigo, DateOnly hoje) => (codigo ?? "hoje") switch
    {
        "hoje" => new Periodo("hoje", hoje, hoje, hoje.AddDays(-1), hoje.AddDays(-1)),
        "7dias" => new Periodo("7dias", hoje.AddDays(-6), hoje, hoje.AddDays(-13), hoje.AddDays(-7)),
        "mes" => Mes(hoje),
        _ => null,
    };

    /// <summary>Do dia 1 até hoje, contra o mesmo trecho do mês anterior (até o último dia dele, se for mais curto).</summary>
    public static Periodo Mes(DateOnly hoje)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        var anterior = inicio.AddMonths(-1);
        var fimAnterior = anterior.AddDays(Math.Min(hoje.Day, DateTime.DaysInMonth(anterior.Year, anterior.Month)) - 1);
        return new Periodo("mes", inicio, hoje, anterior, fimAnterior);
    }
}

/// <summary>Monta o dashboard juntando os indicadores dos módulos (vendas, financeiro, estoque) e os alertas.</summary>
public sealed class Painel(IIndicadoresDeVendas vendas, IIndicadoresFinanceiros financeiro, IIndicadoresDeEstoque estoque)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => $"R$ {valor.ToString("N2", PtBr)}";

    public static decimal? Variacao(decimal atual, decimal anterior) => anterior == 0 ? null : Math.Round((atual - anterior) / anterior * 100, 1);

    public static string Plural(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    public async Task<Dashboard> MontarAsync(IReadOnlyCollection<Guid> empresaIds, string perfil, Guid? usuarioId, Periodo periodo, DateOnly hoje, CancellationToken ct)
    {
        BlocoDeVendas? blocoDeVendas = null;
        var completas = Perfis.VendasCompletas.Contains(perfil);
        if (completas || Perfis.VendasProprias.Contains(perfil))
        {
            var dono = completas ? null : usuarioId;
            var atual = await vendas.VendasAsync(empresaIds, periodo.De, periodo.Ate, dono, ct);
            var anterior = await vendas.VendasAsync(empresaIds, periodo.AnteriorDe, periodo.AnteriorAte, dono, ct);
            var mes = Periodo.Mes(hoje);
            var noMes = periodo.Codigo == "mes" ? atual : await vendas.VendasAsync(empresaIds, mes.De, mes.Ate, dono, ct);
            var mesAnterior = periodo.Codigo == "mes" ? anterior : await vendas.VendasAsync(empresaIds, mes.AnteriorDe, mes.AnteriorAte, dono, ct);
            var trinta = await vendas.VendasAsync(empresaIds, hoje.AddDays(-29), hoje, dono, ct);
            blocoDeVendas = new BlocoDeVendas(Cartao(atual, anterior), Cartao(noMes, mesAnterior), trinta.PorDia, atual.PorForma, atual.PorVendedor,
                atual.MaisVendidos, !completas);
        }

        var resumoFinanceiro = Perfis.Financeiro.Contains(perfil) ? await financeiro.ResumoAsync(empresaIds, hoje, ct) : null;
        var resumoDeEstoque = Perfis.Estoque.Contains(perfil) ? await estoque.ResumoAsync(empresaIds, ct) : null;
        var condicionais = Perfis.Condicionais.Contains(perfil) ? await vendas.CondicionaisAsync(empresaIds, hoje, ct) : null;
        var verCrediario = Perfis.Crediario.Contains(perfil);

        return new Dashboard(periodo.Codigo, periodo.De, periodo.Ate, blocoDeVendas, resumoFinanceiro, resumoDeEstoque, condicionais,
            Alertas(resumoFinanceiro, resumoDeEstoque, condicionais, verCrediario));
    }

    private static Cartao Cartao(ResumoDeVendas atual, ResumoDeVendas anterior) =>
        new(atual.Total, atual.Vendas, atual.Pecas, atual.TicketMedio, anterior.Total, Variacao(atual.Total, anterior.Total));

    public static IReadOnlyList<Alerta> Alertas(ResumoFinanceiro? f, ResumoDeEstoque? e, CondicionaisNaRua? c, bool verCrediario)
    {
        var alertas = new List<Alerta>();
        if (f is { APagar.ParcelasVencidas: > 0 })
            alertas.Add(new Alerta("contas_a_pagar_vencidas", "perigo", $"{Plural(f.APagar.ParcelasVencidas, "conta a pagar vencida", "contas a pagar vencidas")}",
                $"{Moeda(f.APagar.Vencido)} em atraso", "/financeiro/contas-a-pagar"));
        if (c is { Atrasadas: > 0 })
            alertas.Add(new Alerta("condicionais_atrasadas", "perigo", $"{Plural(c.Atrasadas, "condicional atrasada", "condicionais atrasadas")}",
                "Passaram do prazo de devolução", "/vendas/condicional"));
        if (f is { AReceber.ParcelasVencidas: > 0 })
            alertas.Add(new Alerta("contas_a_receber_vencidas", "aviso", $"{Plural(f.AReceber.ParcelasVencidas, "conta a receber vencida", "contas a receber vencidas")}",
                $"{Moeda(f.AReceber.Vencido)} em atraso", "/financeiro/contas-a-receber"));
        if (verCrediario && f is { ClientesEmAtraso: > 0 })
            alertas.Add(new Alerta("crediario_em_atraso", "aviso", $"{Plural(f.ClientesEmAtraso, "cliente", "clientes")} com crediário em atraso",
                $"{Moeda(f.CrediarioEmAtraso)} em parcelas vencidas", "/vendas/crediario"));
        if (e is { SkusNegativos: > 0 })
            alertas.Add(new Alerta("estoque_negativo", "aviso", $"{Plural(e.SkusNegativos, "SKU", "SKUs")} com estoque negativo",
                "Vendidos sem saldo: confira as entradas", "/estoque/posicao"));
        if (e is { TotalDeQuebras: > 0 })
        {
            var primeira = e.Quebras[0];
            alertas.Add(new Alerta("quebra_de_grade", "aviso", $"{Plural(e.TotalDeQuebras, "produto", "produtos")} com quebra de grade",
                $"{primeira.Descricao}: {string.Join(", ", primeira.Esgotados)} {(primeira.Esgotados.Count == 1 ? "esgotado" : "esgotados")}", "/estoque/posicao"));
        }
        return alertas;
    }
}
