using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Infrastructure.Auditoria;

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class AuditoriaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditoriaDbContext>
{
    public AuditoriaDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<AuditoriaDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, AuditoriaDbContext.Schema)
            .Options;
        return new AuditoriaDbContext(options, tenant);
    }
}
