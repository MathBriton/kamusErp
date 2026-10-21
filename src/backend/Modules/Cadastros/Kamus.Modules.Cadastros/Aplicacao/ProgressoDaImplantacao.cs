using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Aplicacao;

/// <summary>Produtos e clientes são do cliente inteiro (valem para todas as empresas).</summary>
internal sealed class ProgressoDaImplantacao(CadastrosDbContext db) : IProgressoDaImplantacao
{
    public async Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default)
    {
        var etapas = new List<string>();
        if (await db.Produtos.AnyAsync(ct)) etapas.Add(EtapasDaImplantacao.Produtos);
        if (await db.Pessoas.AnyAsync(p => p.EhCliente, ct)) etapas.Add(EtapasDaImplantacao.Clientes);
        return etapas;
    }
}
