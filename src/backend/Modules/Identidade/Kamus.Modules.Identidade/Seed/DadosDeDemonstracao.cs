using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Documentos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Modules.Identidade.Seed;

/// <summary>
/// Cliente "Loja Demo" com 2 CNPJs e o usuário admin@kamus.local (perfil Dono nos dois).
/// Roda <b>somente em Development</b>. A senha vem de <c>Seed:SenhaAdmin</c>.
/// </summary>
internal static class DadosDeDemonstracao
{
    public static readonly Guid TenantId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    public const string EmailAdmin = "admin@kamus.local";

    public static async Task CriarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var escopo = services.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<IdentidadeDbContext>();

        if (await db.Tenants.AnyAsync(t => t.Id == TenantId, ct)) return;

        var senha = sp.GetRequiredService<IConfiguration>()["Seed:SenhaAdmin"];
        if (string.IsNullOrWhiteSpace(senha))
            throw new InvalidOperationException("Configure 'Seed:SenhaAdmin' para criar o usuário de teste (variável de ambiente Seed__SenhaAdmin).");

        var agora = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        db.Tenants.Add(Tenant.Criar("Loja Demo", agora, TenantId));
        sp.GetRequiredService<TenantContext>().Definir(TenantId);

        var centro = Empresa.Criar("Kamus Moda Centro LTDA", "Kamus Moda Centro", Cnpj.Parse("12.345.678/0001-95"));
        var shopping = Empresa.Criar("Kamus Moda Shopping LTDA", "Kamus Moda Shopping", Cnpj.Parse("98.765.432/0001-98"));
        db.Empresas.AddRange(centro, shopping);

        var admin = Usuario.Criar(TenantId, "Admin Demo", EmailAdmin);
        admin.DefinirSenhaHash(sp.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(admin, senha));
        db.Usuarios.Add(admin);

        db.AcessosEmpresa.AddRange(
            AcessoEmpresa.Criar(admin.Id, centro.Id, Perfil.Dono),
            AcessoEmpresa.Criar(admin.Id, shopping.Id, Perfil.Dono));

        await db.SaveChangesAsync(ct);
    }
}
