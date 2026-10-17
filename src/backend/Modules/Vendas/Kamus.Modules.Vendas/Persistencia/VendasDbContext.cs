using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Vendas.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Vendas.Persistencia;

public sealed class VendasDbContext(DbContextOptions<VendasDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "vendas";

    public DbSet<FormaDePagamento> Formas => Set<FormaDePagamento>();
    public DbSet<ConfiguracaoDeVendas> Configuracoes => Set<ConfiguracaoDeVendas>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemDaVenda> Itens => Set<ItemDaVenda>();
    public DbSet<PagamentoDaVenda> Pagamentos => Set<PagamentoDaVenda>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<FormaDePagamento>(e =>
        {
            e.ToTable("formas_de_pagamento");
            e.Property(f => f.Nome).HasMaxLength(60);
            e.Property(f => f.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.TaxaAVista).HasPrecision(5, 2);
            e.Property(f => f.TaxaParcelado).HasPrecision(5, 2);
            e.HasIndex(f => new { f.TenantId, f.EmpresaId, f.Nome }).IsUnique();
        });

        modelBuilder.Entity<ConfiguracaoDeVendas>(e =>
        {
            e.ToTable("configuracoes");
            e.HasIndex(c => new { c.TenantId, c.EmpresaId }).IsUnique();
        });

        modelBuilder.Entity<Venda>(e =>
        {
            e.ToTable("vendas");
            e.Property(v => v.ClienteNome).HasMaxLength(150);
            e.Property(v => v.VendedorNome).HasMaxLength(150);
            e.Property(v => v.ComissaoPercentual).HasPrecision(5, 2);
            e.Property(v => v.DescontoGeral).HasPrecision(14, 2);
            e.Property(v => v.Troco).HasPrecision(14, 2);
            e.Property(v => v.NotaExterna).HasMaxLength(60);
            e.Property(v => v.Observacao).HasMaxLength(500);
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.RegistradaPor).HasMaxLength(150);
            e.Property(v => v.MotivoDoCancelamento).HasMaxLength(300);
            e.Property(v => v.CanceladaPor).HasMaxLength(150);
            e.Ignore(v => v.Subtotal);
            e.Ignore(v => v.Total);
            e.Ignore(v => v.Pecas);
            e.Ignore(v => v.Comissao);
            e.Ignore(v => v.TaxaTotal);
            e.Ignore(v => v.CustoTotal);
            e.Ignore(v => v.LucroBruto);
            e.HasIndex(v => new { v.TenantId, v.EmpresaId, v.Numero }).IsUnique();
            e.HasIndex(v => new { v.TenantId, v.EmpresaId, v.Data });
            e.HasMany(v => v.Itens).WithOne().HasForeignKey(i => i.VendaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(v => v.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(v => v.Pagamentos).WithOne().HasForeignKey(p => p.VendaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(v => v.Pagamentos).HasField("_pagamentos").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemDaVenda>(e =>
        {
            e.ToTable("itens_da_venda");
            e.Property(i => i.Descricao).HasMaxLength(200);
            e.Property(i => i.PrecoUnitario).HasPrecision(14, 2);
            e.Property(i => i.Desconto).HasPrecision(14, 2);
            e.Property(i => i.CustoUnitario).HasPrecision(14, 4);
            e.Ignore(i => i.Total);
            e.HasIndex(i => new { i.TenantId, i.SkuId });
        });

        modelBuilder.Entity<PagamentoDaVenda>(e =>
        {
            e.ToTable("pagamentos_da_venda");
            e.Property(p => p.FormaNome).HasMaxLength(60);
            e.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Valor).HasPrecision(14, 2);
            e.Property(p => p.TaxaPercentual).HasPrecision(5, 2);
            e.Ignore(p => p.ValorTaxa);
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class VendasDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VendasDbContext>
{
    public VendasDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<VendasDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, VendasDbContext.Schema)
            .Options;
        return new VendasDbContext(options, tenant);
    }
}
