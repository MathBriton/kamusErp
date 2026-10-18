using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <summary>Crediário próprio: dívidas (títulos a receber marcados como crediário), limite, atraso e encargos do cliente.</summary>
internal sealed class Crediario(FinanceiroDbContext db, TimeProvider relogio) : ICrediario
{
    public DateOnly Hoje => CalendarioBrasilia.Hoje(relogio);

    public async Task<ConfiguracaoDoCrediario> ConfiguracaoAsync(CancellationToken ct) =>
        await db.ConfiguracoesDoCrediario.SingleOrDefaultAsync(ct) ?? ConfiguracaoDoCrediario.Padrao();

    /// <summary>Títulos de crediário não cancelados (de todas as empresas do cliente do SaaS, ou só das informadas).</summary>
    public async Task<List<Titulo>> TitulosAsync(Guid? clienteId, IReadOnlyCollection<Guid>? empresaIds, CancellationToken ct)
    {
        var q = db.Titulos.Include(t => t.Parcelas).ThenInclude(p => p.Baixas).Where(t => t.Crediario && !t.Cancelado);
        if (clienteId is { } c) q = q.Where(t => t.PessoaId == c);
        if (empresaIds is not null) q = q.Where(t => empresaIds.Contains(t.EmpresaId));
        var titulos = await q.ToListAsync(ct);
        foreach (var t in titulos) t.Reconectar();
        return titulos;
    }

    public int DiasDeAtraso(IEnumerable<Titulo> titulos) =>
        titulos.SelectMany(t => t.Parcelas).Where(p => p.Saldo > 0).Select(p => Hoje.DayNumber - p.Vencimento.DayNumber).DefaultIfEmpty(0).Max() is var d && d > 0 ? d : 0;

    public async Task<SituacaoDoCredito> SituacaoAsync(Guid clienteId, CancellationToken ct = default)
    {
        var titulos = await TitulosAsync(clienteId, null, ct);
        var limite = await db.LimitesDeCredito.AsNoTracking().SingleOrDefaultAsync(l => l.ClienteId == clienteId, ct);
        return Credito.Avaliar(limite, titulos.SelectMany(t => t.Parcelas).Sum(p => p.Saldo), DiasDeAtraso(titulos), await ConfiguracaoAsync(ct));
    }
}
