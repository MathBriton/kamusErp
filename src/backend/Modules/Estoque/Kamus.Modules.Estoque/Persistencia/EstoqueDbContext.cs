using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Estoque.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Estoque.Persistencia;

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "estoque";

    public DbSet<Loja> Lojas => Set<Loja>();
    public DbSet<SaldoDeEstoque> Saldos => Set<SaldoDeEstoque>();
    public DbSet<MovimentoDeEstoque> Movimentos => Set<MovimentoDeEstoque>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<ItemDeInventario> ItensDeInventario => Set<ItemDeInventario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Loja>(e =>
        {
            e.ToTable("lojas");
            e.Property(l => l.Nome).HasMaxLength(Loja.TamanhoMaximoDoNome);
            e.HasIndex(l => new { l.TenantId, l.EmpresaId, l.Nome }).IsUnique();
        });

        modelBuilder.Entity<SaldoDeEstoque>(e =>
        {
            e.ToTable("saldos");
            e.Property(s => s.CustoMedio).HasPrecision(14, 4);
            e.Property(s => s.Versao).IsConcurrencyToken();
            e.Ignore(s => s.ValorEmCusto);
            e.HasIndex(s => new { s.TenantId, s.LojaId, s.SkuId }).IsUnique();
            e.HasIndex(s => new { s.TenantId, s.ProdutoId });
            e.HasOne<Loja>().WithMany().HasForeignKey(s => s.LojaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentoDeEstoque>(e =>
        {
            e.ToTable("movimentos");
            e.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(m => m.CustoUnitario).HasPrecision(14, 4);
            e.Property(m => m.CustoMedioApos).HasPrecision(14, 4);
            e.Property(m => m.Motivo).HasMaxLength(40);
            e.Property(m => m.Documento).HasMaxLength(60);
            e.Property(m => m.Observacao).HasMaxLength(500);
            e.Property(m => m.UsuarioNome).HasMaxLength(150);
            e.HasIndex(m => new { m.TenantId, m.SkuId, m.OcorridoEm });
            e.HasIndex(m => new { m.TenantId, m.ProdutoId, m.OcorridoEm });
            e.HasIndex(m => m.OperacaoId);
            e.HasOne<Loja>().WithMany().HasForeignKey(m => m.LojaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Inventario>(e =>
        {
            e.ToTable("inventarios");
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Observacao).HasMaxLength(500);
            e.Property(i => i.AbertoPor).HasMaxLength(150);
            e.Property(i => i.ConcluidoPor).HasMaxLength(150);
            e.HasIndex(i => new { i.TenantId, i.LojaId, i.AbertoEm });
            e.HasOne<Loja>().WithMany().HasForeignKey(i => i.LojaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(i => i.Itens).WithOne().HasForeignKey(i => i.InventarioId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(i => i.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemDeInventario>(e =>
        {
            e.ToTable("itens_de_inventario");
            e.HasIndex(i => new { i.InventarioId, i.SkuId }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class EstoqueDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EstoqueDbContext>
{
    public EstoqueDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<EstoqueDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, EstoqueDbContext.Schema)
            .Options;
        return new EstoqueDbContext(options, tenant);
    }
}
