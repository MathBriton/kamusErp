using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

internal sealed class ProgressoDaImplantacao(VendasDbContext db) : IProgressoDaImplantacao
{
    public async Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default)
    {
        var etapas = new List<string>();
        // Formas prontas: há forma ativa e toda forma ativa que entra na hora (dinheiro, PIX) já tem a conta onde o dinheiro cai.
        var formas = await db.Formas.AsNoTracking().Where(f => empresaIds.Contains(f.EmpresaId) && f.Ativa).ToListAsync(ct);
        if (formas.Count > 0 && formas.All(f => !f.EntraNaHora || f.ContaId != null)) etapas.Add(EtapasDaImplantacao.FormasDePagamento);
        if (await db.Vendas.AnyAsync(v => empresaIds.Contains(v.EmpresaId), ct)) etapas.Add(EtapasDaImplantacao.PrimeiraVenda);
        return etapas;
    }
}
