using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Notificacoes.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Notificacoes.Persistencia;

public sealed class NotificacoesDbContext(DbContextOptions<NotificacoesDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "notificacoes";

    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("notificacoes");
            e.Property(n => n.Tipo).HasMaxLength(50);
            e.Property(n => n.Titulo).HasMaxLength(150);
            e.Property(n => n.Mensagem).HasMaxLength(1000);
            e.Property(n => n.Link).HasMaxLength(300);
            e.HasIndex(n => new { n.UsuarioId, n.LidaEm, n.CriadaEm });
        });
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class NotificacoesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificacoesDbContext>
{
    public NotificacoesDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<NotificacoesDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, NotificacoesDbContext.Schema)
            .Options;
        return new NotificacoesDbContext(options, tenant);
    }
}
