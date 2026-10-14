using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Licenciamento.Persistencia;

/// <summary>
/// Licenças e pagamentos são dados do <b>operador do SaaS</b>, não do cliente: não têm RLS (o Painel
/// Administrativo enxerga todos os clientes). Do lado do cliente, toda consulta filtra pelo tenant explicitamente.
/// </summary>
public sealed class LicenciamentoDbContext(DbContextOptions<LicenciamentoDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "licenciamento";

    public DbSet<Licenca> Licencas => Set<Licenca>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Licenca>(e =>
        {
            e.ToTable("licencas");
            e.HasKey(l => l.TenantId);
            e.Property(l => l.TenantId).ValueGeneratedNever();
            e.Property(l => l.Plano).HasMaxLength(50);
            e.Property(l => l.ValorMensal).HasPrecision(10, 2);
            e.Property(l => l.UltimoAviso).HasMaxLength(100);
            e.Ignore(l => l.Vencimento);
            e.Ignore(l => l.FimDaCarencia);
        });

        modelBuilder.Entity<Pagamento>(e =>
        {
            e.ToTable("pagamentos");
            e.Property(p => p.Valor).HasPrecision(10, 2);
            e.Property(p => p.Forma).HasMaxLength(50);
            e.Property(p => p.Observacao).HasMaxLength(500);
            e.Property(p => p.RegistradoPor).HasMaxLength(254);
            e.HasIndex(p => new { p.TenantId, p.PagoEm });
            e.HasOne<Licenca>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class LicenciamentoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LicenciamentoDbContext>
{
    public LicenciamentoDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<LicenciamentoDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, LicenciamentoDbContext.Schema)
            .Options;
        return new LicenciamentoDbContext(options, tenant);
    }
}
