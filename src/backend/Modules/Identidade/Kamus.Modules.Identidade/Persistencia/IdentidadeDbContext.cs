using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Identidade.Dominio;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Identidade.Persistencia;

public sealed class IdentidadeDbContext(DbContextOptions<IdentidadeDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "identidade";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AcessoEmpresa> AcessosEmpresa => Set<AcessoEmpresa>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.Property(t => t.Nome).HasMaxLength(150);
        });

        modelBuilder.Entity<Empresa>(e =>
        {
            e.ToTable("empresas");
            e.Property(x => x.RazaoSocial).HasMaxLength(150);
            e.Property(x => x.NomeFantasia).HasMaxLength(150);
            e.Property(x => x.Cnpj).HasMaxLength(14).HasConversion(c => c.Valor, v => Cnpj.Parse(v));
            e.HasIndex(x => new { x.TenantId, x.Cnpj }).IsUnique();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.Property(u => u.Nome).HasMaxLength(150);
            e.Property(u => u.Email).HasMaxLength(254);
            e.Property(u => u.SenhaHash).HasMaxLength(500);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AcessoEmpresa>(e =>
        {
            e.ToTable("acessos_empresa");
            e.Property(a => a.Perfil).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => new { a.UsuarioId, a.EmpresaId }).IsUnique();
            e.HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Empresa>().WithMany().HasForeignKey(a => a.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(r => r.TokenHash).HasMaxLength(64);
            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => r.UsuarioId);
            e.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
