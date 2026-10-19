using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Indicadores.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Indicadores.Persistencia;

public sealed class IndicadoresDbContext(DbContextOptions<IndicadoresDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "indicadores";

    public DbSet<PreferenciasDoResumo> Preferencias => Set<PreferenciasDoResumo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<PreferenciasDoResumo>(e =>
        {
            e.ToTable("preferencias_do_resumo");
            e.HasIndex(p => new { p.TenantId, p.UsuarioId }).IsUnique();
        });
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class IndicadoresDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IndicadoresDbContext>
{
    public IndicadoresDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<IndicadoresDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, IndicadoresDbContext.Schema)
            .Options;
        return new IndicadoresDbContext(options, tenant);
    }
}
