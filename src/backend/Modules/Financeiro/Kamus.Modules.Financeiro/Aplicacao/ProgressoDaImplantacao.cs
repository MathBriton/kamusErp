using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

internal sealed class ProgressoDaImplantacao(FinanceiroDbContext db) : IProgressoDaImplantacao
{
    public async Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default) =>
        await db.Titulos.AnyAsync(t => empresaIds.Contains(t.EmpresaId) && t.Tipo == TipoDeTitulo.Pagar, ct) ? [EtapasDaImplantacao.ContasAPagar] : [];
}
