using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

internal sealed class ProgressoDaImplantacao(VendasDbContext db) : IProgressoDaImplantacao
{
    public async Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default) =>
        await db.Vendas.AnyAsync(v => empresaIds.Contains(v.EmpresaId), ct) ? [EtapasDaImplantacao.PrimeiraVenda] : [];
}
