using System.Text.Json;
using Kamus.Infrastructure.Persistence;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kamus.Infrastructure.Auditoria;

public sealed class AuditoriaDbContext(DbContextOptions<AuditoriaDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "auditoria";

    public DbSet<RegistroAuditoria> Registros => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<RegistroAuditoria>(e =>
        {
            e.ToTable("registros");
            e.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Evento).HasMaxLength(50);
            e.Property(r => r.Entidade).HasMaxLength(100);
            e.Property(r => r.EntidadeId).HasMaxLength(100);
            e.Property(r => r.Descricao).HasMaxLength(500);
            e.Property(r => r.UsuarioNome).HasMaxLength(150);
            e.Property(r => r.Ip).HasMaxLength(45);
            e.Property(r => r.Alteracoes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<List<AlteracaoDeCampo>>(v, JsonSerializerOptions.Default) ?? new List<AlteracaoDeCampo>(),
                    new ValueComparer<List<AlteracaoDeCampo>>(
                        (a, b) => a!.SequenceEqual(b!),
                        v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
                        v => v.ToList()));
            e.HasIndex(r => new { r.TenantId, r.OcorridoEm });
            e.HasIndex(r => new { r.Entidade, r.EntidadeId });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GarantirSomenteInclusao();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GarantirSomenteInclusao();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    // No PostgreSQL o kamus_app também não tem permissão de UPDATE/DELETE nesta tabela.
    private void GarantirSomenteInclusao()
    {
        if (ChangeTracker.Entries<RegistroAuditoria>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("O histórico de alterações não pode ser alterado nem apagado.");
    }
}
