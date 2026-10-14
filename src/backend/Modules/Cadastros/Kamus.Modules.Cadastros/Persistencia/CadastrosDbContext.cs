using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Cadastros.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kamus.Modules.Cadastros.Persistencia;

public sealed class CadastrosDbContext(DbContextOptions<CadastrosDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "cadastros";

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<GradeDeTamanhos> Grades => Set<GradeDeTamanhos>();
    public DbSet<Cor> Cores => Set<Cor>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Sku> Skus => Set<Sku>();

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

        modelBuilder.Entity<GradeDeTamanhos>(e =>
        {
            e.ToTable("grades");
            e.Property(g => g.Nome).HasMaxLength(60);
            // Tamanhos em uma coluna de texto ("PP|P|M|G|GG"): a ordem é a da lista.
            e.Property(g => g.Tamanhos).HasMaxLength(400).HasConversion(
                new ValueConverter<IReadOnlyList<string>, string>(t => string.Join('|', t), s => s.Split('|', StringSplitOptions.None)),
                new ValueComparer<IReadOnlyList<string>>((a, b) => a!.SequenceEqual(b!), t => t.Aggregate(0, (h, x) => HashCode.Combine(h, x)), t => t.ToList()));
            e.HasIndex(g => new { g.TenantId, g.Nome }).IsUnique();
        });

        modelBuilder.Entity<Cor>(e =>
        {
            e.ToTable("cores");
            e.Property(c => c.Nome).HasMaxLength(40);
            e.Property(c => c.Hex).HasMaxLength(7);
            e.HasIndex(c => new { c.TenantId, c.Nome }).IsUnique();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("produtos");
            e.Property(p => p.Referencia).HasMaxLength(30);
            e.Property(p => p.Descricao).HasMaxLength(150);
            e.Property(p => p.Marca).HasMaxLength(60);
            e.Property(p => p.Categoria).HasMaxLength(60);
            e.Property(p => p.Colecao).HasMaxLength(60);
            e.Property(p => p.ReferenciaDoFornecedor).HasMaxLength(40);
            e.Property(p => p.PrecoVenda).HasPrecision(12, 2);
            e.Property(p => p.PrecoCusto).HasPrecision(12, 2);
            e.Ignore(p => p.Markup);
            e.HasIndex(p => new { p.TenantId, p.Referencia }).IsUnique();
            e.HasOne<GradeDeTamanhos>().WithMany().HasForeignKey(p => p.GradeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pessoa>().WithMany().HasForeignKey(p => p.FornecedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Skus).WithOne().HasForeignKey(s => s.ProdutoId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(p => p.Skus).HasField("_skus").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Sku>(e =>
        {
            e.ToTable("skus");
            e.Property(s => s.Tamanho).HasMaxLength(10);
            e.Property(s => s.CodigoDeBarras).HasMaxLength(14);
            e.Property(s => s.PrecoVenda).HasPrecision(12, 2);
            e.HasIndex(s => new { s.TenantId, s.CodigoDeBarras }).IsUnique();
            e.HasIndex(s => new { s.ProdutoId, s.CorId, s.Tamanho }).IsUnique();
            e.HasOne<Cor>().WithMany().HasForeignKey(s => s.CorId).OnDelete(DeleteBehavior.Restrict);
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
