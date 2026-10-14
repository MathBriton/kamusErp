using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Identidade.Busca;

/// <summary>
/// Empresas que o usuário acessa (escolher troca o CNPJ ativo) e, para Dono/Gerente, usuários do cliente.
/// Volumes pequenos: o filtro sem acento roda em memória. Tabelas grandes (produtos) usarão unaccent + pg_trgm.
/// </summary>
internal sealed class ProvedorDeBuscaIdentidade(
    IdentidadeDbContext db, IUsuarioAtual usuario, IEmpresaAtiva empresaAtiva, ITenantContext tenant) : IProvedorDeBusca
{
    private static readonly string[] PerfisQueVeemUsuarios = ["Dono", "Gerente"];

    public async Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(string termo, int limite, CancellationToken ct)
    {
        if (usuario.Id is not { } usuarioId || tenant.TenantId is not { } tenantId) return [];
        var resultados = new List<ResultadoDeBusca>();

        var empresas = await (
            from a in db.AcessosEmpresa.AsNoTracking()
            join e in db.Empresas.AsNoTracking() on a.EmpresaId equals e.Id
            where a.UsuarioId == usuarioId
            select e).ToListAsync(ct);
        resultados.AddRange(empresas
            .Where(e => TextoParaBusca.Contem(e.NomeFantasia, termo) || TextoParaBusca.Contem(e.RazaoSocial, termo) || TextoParaBusca.ContemDigitos(e.Cnpj.Valor, termo))
            .OrderBy(e => e.NomeFantasia, StringComparer.CurrentCulture)
            .Take(limite)
            .Select(e => new ResultadoDeBusca("Empresas", "empresa", e.Id.ToString(), e.NomeFantasia, e.Cnpj.Formatado, null)));

        if (PerfisQueVeemUsuarios.Contains(empresaAtiva.Perfil))
        {
            // Usuario não é ITenantOwned (é lido no login): o filtro por tenant aqui é explícito.
            var usuarios = await db.Usuarios.AsNoTracking().Where(u => u.TenantId == tenantId && u.Ativo).ToListAsync(ct);
            resultados.AddRange(usuarios
                .Where(u => TextoParaBusca.Contem(u.Nome, termo) || TextoParaBusca.Contem(u.Email, termo))
                .OrderBy(u => u.Nome, StringComparer.CurrentCulture)
                .Take(limite)
                .Select(u => new ResultadoDeBusca("Usuários", "usuario", u.Id.ToString(), u.Nome, u.Email, "/configuracoes/usuarios")));
        }

        return resultados;
    }
}
