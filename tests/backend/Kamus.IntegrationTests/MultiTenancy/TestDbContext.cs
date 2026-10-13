using Kamus.Infrastructure.Persistence;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.IntegrationTests.MultiTenancy;

public sealed class Nota : ITenantOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string Texto { get; set; } = "";
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "testes";

    public DbSet<Nota> Notas => Set<Nota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Nota>(e =>
        {
            e.ToTable("notas");
            e.Property(n => n.Texto).HasMaxLength(200);
        });
        base.OnModelCreating(modelBuilder);
    }
}
