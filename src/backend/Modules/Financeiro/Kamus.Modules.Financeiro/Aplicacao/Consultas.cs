using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <param name="Situacao">abertas (inclui vencidas e parciais), vencidas, pagas, canceladas ou todas.</param>
/// <param name="ParcelaIds">Só estas parcelas (linhas selecionadas na grade); nulo = sem esse filtro.</param>
internal sealed record FiltroDeTitulos(TipoDeTitulo Tipo, string Situacao, DateOnly? De, DateOnly? Ate, string? Busca, IReadOnlySet<Guid>? ParcelaIds = null);

internal sealed record LinhaDeParcela(Guid TituloId, Guid ParcelaId, int Numero, int Parcelas, DateOnly Vencimento, string Descricao, string? PessoaNome,
    string? Documento, string? Categoria, decimal Valor, decimal ValorPago, decimal Saldo, string Situacao, string Origem);

internal sealed record ResultadoDosTitulos(IReadOnlyList<LinhaDeParcela> Itens, decimal Valor, decimal Pago, decimal Saldo, decimal Vencido);

/// <summary>Consultas compartilhadas pela tela e pelos relatórios (uma linha por parcela).</summary>
internal static class Consultas
{
    public static readonly string[] Situacoes = ["abertas", "vencidas", "pagas", "canceladas", "todas"];

    public static async Task<ResultadoDosTitulos> ParcelasAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, FiltroDeTitulos f, DateOnly hoje, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var consulta = db.Titulos.AsNoTracking().Include(t => t.Parcelas).ThenInclude(p => p.Baixas).Where(t => ids.Contains(t.EmpresaId) && t.Tipo == f.Tipo);
        var titulos = await consulta.ToListAsync(ct);
        foreach (var t in titulos) t.Reconectar();
        var categorias = await db.Categorias.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var termo = TextoParaBusca.Normalizar(f.Busca);

        var linhas = titulos
            .Where(t => termo.Length == 0 || TextoParaBusca.Normalizar($"{t.Descricao} {t.PessoaNome} {t.Documento}").Contains(termo, StringComparison.Ordinal))
            .SelectMany(t => t.Parcelas.Select(p => (Titulo: t, Parcela: p, Situacao: p.Situacao(hoje))))
            .Where(x => f.ParcelaIds is null || f.ParcelaIds.Contains(x.Parcela.Id))
            .Where(x => (f.De is null || x.Parcela.Vencimento >= f.De) && (f.Ate is null || x.Parcela.Vencimento <= f.Ate))
            .Where(x => f.Situacao switch
            {
                "abertas" => x.Situacao is SituacaoDaParcela.Aberta or SituacaoDaParcela.Vencida or SituacaoDaParcela.Parcial,
                "vencidas" => x.Situacao == SituacaoDaParcela.Vencida,
                "pagas" => x.Situacao == SituacaoDaParcela.Paga,
                "canceladas" => x.Situacao == SituacaoDaParcela.Cancelada,
                _ => true,
            })
            .OrderBy(x => x.Parcela.Vencimento).ThenBy(x => x.Titulo.Descricao, StringComparer.CurrentCulture).ThenBy(x => x.Parcela.Numero)
            .Select(x => new LinhaDeParcela(x.Titulo.Id, x.Parcela.Id, x.Parcela.Numero, x.Titulo.Parcelas.Count, x.Parcela.Vencimento, x.Titulo.Descricao,
                x.Titulo.PessoaNome, x.Titulo.Documento, x.Titulo.CategoriaId is { } c ? categorias.GetValueOrDefault(c) : null, x.Parcela.Valor,
                x.Parcela.ValorPago, x.Situacao == SituacaoDaParcela.Cancelada ? 0 : x.Parcela.Saldo, x.Situacao.ToString(), x.Titulo.Origem))
            .ToList();

        return new ResultadoDosTitulos(linhas, linhas.Sum(l => l.Valor), linhas.Sum(l => l.ValorPago), linhas.Sum(l => l.Saldo),
            linhas.Where(l => l.Situacao == nameof(SituacaoDaParcela.Vencida)).Sum(l => l.Saldo));
    }

    internal sealed record PeriodoDoFluxo(DateOnly Data, decimal EntradasRealizadas, decimal SaidasRealizadas, decimal EntradasPrevistas, decimal SaidasPrevistas, decimal Saldo);

    internal sealed record Fluxo(DateOnly De, DateOnly Ate, string Agrupamento, decimal SaldoInicial, IReadOnlyList<PeriodoDoFluxo> Periodos,
        decimal AReceberEmAtraso, decimal APagarEmAtraso, decimal SaldoFinal);

    /// <summary>
    /// Fluxo de caixa: realizado = movimentos das contas (até hoje); previsto = saldo das parcelas abertas com vencimento de hoje em diante.
    /// Parcelas vencidas ficam em "em atraso" (fora da projeção). Transferências entre contas só entram quando o filtro é uma conta.
    /// </summary>
    public static async Task<Fluxo> FluxoAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, DateOnly de, DateOnly ate, Guid? contaId, DateOnly hoje, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var contas = await db.Contas.AsNoTracking().Where(c => ids.Contains(c.EmpresaId) && (contaId == null || c.Id == contaId)).ToListAsync(ct);
        var contaIds = contas.Select(c => c.Id).ToList();
        var movimentos = await db.Movimentos.AsNoTracking().Where(m => contaIds.Contains(m.ContaId) && m.Data <= ate).ToListAsync(ct);
        if (contaId is null) movimentos = movimentos.Where(m => m.Origem != OrigemDoMovimento.Transferencia).ToList();
        var saldoInicial = contas.Sum(c => c.SaldoInicial) + movimentos.Where(m => m.Data < de).Sum(m => m.Valor);

        var titulos = contaId is not null ? [] : await db.Titulos.AsNoTracking().Include(t => t.Parcelas).ThenInclude(p => p.Baixas)
            .Where(t => ids.Contains(t.EmpresaId) && !t.Cancelado).ToListAsync(ct);
        var parcelas = titulos.SelectMany(t => t.Parcelas.Where(p => p.Saldo > 0).Select(p => (Sinal: t.Tipo == TipoDeTitulo.Receber ? 1m : -1m, p.Vencimento, p.Saldo))).ToList();

        var porMes = ate.DayNumber - de.DayNumber > 62;
        DateOnly Chave(DateOnly d) => porMes ? new DateOnly(d.Year, d.Month, 1) : d;
        var datas = new List<DateOnly>();
        for (var d = Chave(de); d <= ate; d = porMes ? d.AddMonths(1) : d.AddDays(1)) datas.Add(d);

        var realizados = movimentos.Where(m => m.Data >= de && m.Data <= hoje).ToLookup(m => Chave(m.Data));
        var previstos = parcelas.Where(p => p.Vencimento >= hoje && p.Vencimento >= de && p.Vencimento <= ate).ToLookup(p => Chave(p.Vencimento));
        var saldo = saldoInicial;
        var periodos = datas.Select(d =>
        {
            var (er, sr) = (realizados[d].Where(m => m.Valor > 0).Sum(m => m.Valor), -realizados[d].Where(m => m.Valor < 0).Sum(m => m.Valor));
            var (ep, sp) = (previstos[d].Where(p => p.Sinal > 0).Sum(p => p.Saldo), previstos[d].Where(p => p.Sinal < 0).Sum(p => p.Saldo));
            saldo += er - sr + ep - sp;
            return new PeriodoDoFluxo(d, er, sr, ep, sp, saldo);
        }).ToList();

        var atrasadas = parcelas.Where(p => p.Vencimento < hoje).ToList();
        return new Fluxo(de, ate, porMes ? "mes" : "dia", saldoInicial, periodos, atrasadas.Where(p => p.Sinal > 0).Sum(p => p.Saldo),
            atrasadas.Where(p => p.Sinal < 0).Sum(p => p.Saldo), saldo);
    }
}
