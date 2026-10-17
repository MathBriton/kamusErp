using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Aplicacao;

internal sealed class DiretorioDeVendedores(CadastrosDbContext db) : IDiretorioDeVendedores
{
    public async Task<VendedorResumo?> ObterAsync(Guid id, CancellationToken ct = default) =>
        await db.Vendedores.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new VendedorResumo(v.Id, v.Nome, v.ComissaoPercentual, v.Ativo, v.UsuarioId)).SingleOrDefaultAsync(ct);
}
