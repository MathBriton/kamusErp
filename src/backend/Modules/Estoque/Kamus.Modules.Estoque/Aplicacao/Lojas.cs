using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Aplicacao;

/// <summary>Lojas visíveis na requisição. Toda empresa tem ao menos uma loja: a primeira nasce no primeiro acesso, com o nome fantasia.</summary>
internal sealed class Lojas(EstoqueDbContext db, IEmpresaAtiva empresa, TimeProvider relogio)
{
    /// <summary>Lojas da empresa ativa (ou de todas, na visão consolidada), garantindo a loja padrão.</summary>
    public async Task<List<Loja>> VisiveisAsync(CancellationToken ct)
    {
        if (empresa.EmpresaId is { } id && empresa.Nome is { } nome) return await DaEmpresaAsync(id, nome, ct);
        var ids = empresa.EmpresaIds;
        return await db.Lojas.Where(l => ids.Contains(l.EmpresaId)).OrderBy(l => l.Nome).ToListAsync(ct);
    }

    public async Task<List<Loja>> DaEmpresaAsync(Guid empresaId, string nomePadrao, CancellationToken ct)
    {
        var lojas = await db.Lojas.Where(l => l.EmpresaId == empresaId).OrderBy(l => l.Nome).ToListAsync(ct);
        if (lojas.Count > 0) return lojas;
        var padrao = Loja.Criar(empresaId, nomePadrao.Length > Loja.TamanhoMaximoDoNome ? nomePadrao[..Loja.TamanhoMaximoDoNome] : nomePadrao, relogio.GetUtcNow());
        db.Lojas.Add(padrao);
        try
        {
            await db.SaveChangesAsync(ct);
            return [padrao];
        }
        catch (DbUpdateException)
        {
            // Outra requisição criou a loja padrão ao mesmo tempo.
            db.Entry(padrao).State = EntityState.Detached;
            return await db.Lojas.Where(l => l.EmpresaId == empresaId).OrderBy(l => l.Nome).ToListAsync(ct);
        }
    }

    /// <summary>Loja da empresa ativa (para movimentar). Nula se não existe ou é de outra empresa.</summary>
    public async Task<Loja?> DaEmpresaAtivaAsync(Guid lojaId, CancellationToken ct) =>
        (await VisiveisAsync(ct)).FirstOrDefault(l => l.Id == lojaId && l.EmpresaId == empresa.EmpresaId);
}
