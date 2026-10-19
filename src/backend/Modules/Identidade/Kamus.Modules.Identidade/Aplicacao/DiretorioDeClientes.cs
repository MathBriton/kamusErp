using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Modules.Identidade.Aplicacao;

internal sealed class DiretorioDeClientes(IdentidadeDbContext db, IServiceScopeFactory escopos, TenantContext tenant) : IDiretorioDeClientes
{
    public async Task<IReadOnlyList<ClienteDoSaas>> ListarAsync(CancellationToken ct = default)
    {
        // tenants e usuarios não têm RLS (são lidos no login); os acessos (com RLS) são lidos com o tenant de cada cliente.
        var tenants = await db.Tenants.AsNoTracking().OrderBy(t => t.Nome).ToListAsync(ct);
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => u.Ativo).Select(u => new { u.Id, u.TenantId, u.Email }).ToListAsync(ct);

        var clientes = new List<ClienteDoSaas>(tenants.Count);
        foreach (var tenant in tenants)
        {
            var donos = await DonosAsync(tenant.Id, ct);
            var doTenant = usuarios.Where(u => u.TenantId == tenant.Id).ToList();
            var emailDoDono = doTenant.Where(u => donos.Contains(u.Id)).Select(u => u.Email).Order(StringComparer.Ordinal).FirstOrDefault();
            clientes.Add(new ClienteDoSaas(tenant.Id, tenant.Nome, tenant.CriadoEm, doTenant.Count, emailDoDono));
        }
        return clientes;
    }

    public async Task<IReadOnlyList<Guid>> DonosDoTenantAtualAsync(CancellationToken ct = default)
    {
        var ativos = db.Usuarios.AsNoTracking().Where(u => u.Ativo).Select(u => u.Id);
        return await db.AcessosEmpresa.AsNoTracking()
            .Where(a => a.Perfil == Perfil.Dono && ativos.Contains(a.UsuarioId))
            .Select(a => a.UsuarioId).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UsuarioDoCliente>> UsuariosDoTenantAtualAsync(CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant não definido.");
        return await db.Usuarios.AsNoTracking().Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioDoCliente(u.Id, u.Nome, u.Email, u.Ativo)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AcessoDoUsuario>> AcessosDoTenantAtualAsync(CancellationToken ct = default)
    {
        var ativos = db.Usuarios.AsNoTracking().Where(u => u.Ativo).Select(u => u.Id);
        var acessos = await db.AcessosEmpresa.AsNoTracking().Where(a => ativos.Contains(a.UsuarioId)).ToListAsync(ct);
        var empresas = await db.Empresas.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.NomeFantasia, ct);
        return acessos.Where(a => empresas.ContainsKey(a.EmpresaId))
            .Select(a => new AcessoDoUsuario(a.UsuarioId, a.EmpresaId, empresas[a.EmpresaId], a.Perfil.ToString())).ToList();
    }

    private async Task<IReadOnlyList<Guid>> DonosAsync(Guid tenantId, CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(tenantId);
        return await escopo.ServiceProvider.GetRequiredService<IDiretorioDeClientes>().DonosDoTenantAtualAsync(ct);
    }
}
