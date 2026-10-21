using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Aplicacao;

/// <summary>Entrada de estoque concluída = alguma entrada (manual, por XML) ou estoque inicial nas lojas das empresas.</summary>
internal sealed class ProgressoDaImplantacao(EstoqueDbContext db) : IProgressoDaImplantacao
{
    public async Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default)
    {
        var lojas = db.Lojas.Where(l => empresaIds.Contains(l.EmpresaId)).Select(l => l.Id);
        var temEntrada = await db.Movimentos.AnyAsync(m => lojas.Contains(m.LojaId) && (m.Tipo == TipoDeMovimento.Entrada || m.Tipo == TipoDeMovimento.EstoqueInicial), ct);
        return temEntrada ? [EtapasDaImplantacao.EntradaDeEstoque] : [];
    }
}
