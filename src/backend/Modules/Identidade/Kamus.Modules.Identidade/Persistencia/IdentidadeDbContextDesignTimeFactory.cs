using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Identidade.Persistencia;

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class IdentidadeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentidadeDbContext>
{
    public IdentidadeDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<IdentidadeDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, IdentidadeDbContext.Schema)
            .Options;
        return new IdentidadeDbContext(options, tenant);
    }
}
