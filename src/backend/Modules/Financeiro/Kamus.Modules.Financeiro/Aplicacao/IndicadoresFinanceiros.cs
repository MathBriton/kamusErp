using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Endpoints;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <summary>Saldos das contas e compromissos (a pagar/receber de hoje, amanhã, 7 dias e vencidos) para o dashboard e o resumo diário.</summary>
internal sealed class IndicadoresFinanceiros(FinanceiroDbContext db) : IIndicadoresFinanceiros
{
    public async Task<ResumoFinanceiro> ResumoAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly hoje, CancellationToken ct = default)
    {
        var contas = await db.Contas.AsNoTracking().Where(c => empresaIds.Contains(c.EmpresaId) && c.Ativa).OrderBy(c => c.Nome).ToListAsync(ct);
        var saldos = await ContasEndpoints.SaldosAsync(db, contas, null, ct);

        var titulos = await db.Titulos.AsNoTracking().Include(t => t.Parcelas).ThenInclude(p => p.Baixas)
            .Where(t => empresaIds.Contains(t.EmpresaId) && !t.Cancelado).ToListAsync(ct);
        var abertas = titulos.SelectMany(t => t.Parcelas.Where(p => p.Saldo > 0).Select(p => (Titulo: t, Parcela: p))).ToList();

        Compromissos Compromissos(TipoDeTitulo tipo)
        {
            var doTipo = abertas.Where(x => x.Titulo.Tipo == tipo).Select(x => x.Parcela).ToList();
            var vencidas = doTipo.Where(p => p.Vencimento < hoje).ToList();
            return new Compromissos(
                doTipo.Where(p => p.Vencimento == hoje).Sum(p => p.Saldo),
                doTipo.Where(p => p.Vencimento == hoje.AddDays(1)).Sum(p => p.Saldo),
                doTipo.Where(p => p.Vencimento >= hoje && p.Vencimento <= hoje.AddDays(6)).Sum(p => p.Saldo),
                vencidas.Sum(p => p.Saldo),
                vencidas.Count);
        }

        var crediarioVencido = abertas.Where(x => x.Titulo.Crediario && x.Parcela.Vencimento < hoje).ToList();
        return new ResumoFinanceiro(
            contas.Select(c => new SaldoDeConta(c.Id, c.Nome, c.Tipo.ToString(), saldos[c.Id])).ToList(),
            saldos.Values.Sum(),
            Compromissos(TipoDeTitulo.Receber),
            Compromissos(TipoDeTitulo.Pagar),
            crediarioVencido.Sum(x => x.Parcela.Saldo),
            crediarioVencido.Select(x => x.Titulo.PessoaId?.ToString() ?? x.Titulo.PessoaNome).Distinct().Count());
    }
}
