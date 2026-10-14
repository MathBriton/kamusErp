using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Cadastros.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Cadastros.Persistencia;

public sealed class CadastrosDbContext(DbContextOptions<CadastrosDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "cadastros";

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Pessoa>(e =>
        {
            e.ToTable("pessoas");
            e.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.Nome).HasMaxLength(150);
            e.Property(p => p.NomeFantasia).HasMaxLength(150);
            e.Property(p => p.Documento).HasMaxLength(14);
            e.Property(p => p.Telefone).HasMaxLength(11);
            e.Property(p => p.Email).HasMaxLength(254);
            e.Property(p => p.Observacoes).HasMaxLength(2000);
            e.Property(p => p.EnderecoCep).HasMaxLength(9);
            e.Property(p => p.EnderecoLogradouro).HasMaxLength(150);
            e.Property(p => p.EnderecoNumero).HasMaxLength(20);
            e.Property(p => p.EnderecoComplemento).HasMaxLength(100);
            e.Property(p => p.EnderecoBairro).HasMaxLength(100);
            e.Property(p => p.EnderecoCidade).HasMaxLength(100);
            e.Property(p => p.EnderecoUf).HasMaxLength(2);
            e.Property(p => p.PreferenciaBlusa).HasMaxLength(10);
            e.Property(p => p.PreferenciaCalca).HasMaxLength(10);
            e.Property(p => p.PreferenciaCalcado).HasMaxLength(10);
            e.Property(p => p.PreferenciaEstilo).HasMaxLength(300);
            e.Ignore(p => p.Endereco);
            e.Ignore(p => p.Preferencias);
            e.Ignore(p => p.WhatsApp);
            e.Ignore(p => p.DocumentoFormatado);
            e.HasIndex(p => new { p.TenantId, p.Documento }).IsUnique().HasFilter("documento IS NOT NULL");
            e.HasIndex(p => new { p.TenantId, p.Nome });
        });

        modelBuilder.Entity<Vendedor>(e =>
        {
            e.ToTable("vendedores");
            e.Property(v => v.Nome).HasMaxLength(150);
            e.Property(v => v.Telefone).HasMaxLength(11);
            e.Property(v => v.ComissaoPercentual).HasPrecision(5, 2);
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class CadastrosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CadastrosDbContext>
{
    public CadastrosDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<CadastrosDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, CadastrosDbContext.Schema)
            .Options;
        return new CadastrosDbContext(options, tenant);
    }
}
