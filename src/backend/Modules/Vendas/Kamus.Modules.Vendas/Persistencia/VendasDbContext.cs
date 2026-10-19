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
    public DbSet<Troca> Trocas => Set<Troca>();
    public DbSet<ItemDevolvidoNaTroca> ItensDevolvidos => Set<ItemDevolvidoNaTroca>();
    public DbSet<ItemLevadoNaTroca> ItensLevados => Set<ItemLevadoNaTroca>();
    public DbSet<PagamentoDaTroca> PagamentosDaTroca => Set<PagamentoDaTroca>();
    public DbSet<ValeTroca> Vales => Set<ValeTroca>();
    public DbSet<FechamentoDeComissao> Fechamentos => Set<FechamentoDeComissao>();
    public DbSet<Condicional> Condicionais => Set<Condicional>();
    public DbSet<ItemDaCondicional> ItensDaCondicional => Set<ItemDaCondicional>();

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
            e.Property(c => c.LimiteCondicionalValor).HasPrecision(14, 2);
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
            e.HasIndex(v => new { v.TenantId, v.ClienteId });
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

        modelBuilder.Entity<Troca>(e =>
        {
            e.ToTable("trocas");
            e.Property(t => t.ClienteNome).HasMaxLength(150);
            e.Property(t => t.VendedorNome).HasMaxLength(150);
            e.Property(t => t.ComissaoPercentual).HasPrecision(5, 2);
            e.Property(t => t.Motivo).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Observacao).HasMaxLength(500);
            e.Property(t => t.Troco).HasPrecision(14, 2);
            e.Property(t => t.RegistradaPor).HasMaxLength(150);
            e.Ignore(t => t.ValorDevolvido);
            e.Ignore(t => t.ValorLevado);
            e.Ignore(t => t.Diferenca);
            e.Ignore(t => t.ValorDoVale);
            e.Ignore(t => t.Comissao);
            e.Ignore(t => t.TaxaTotal);
            e.HasIndex(t => new { t.TenantId, t.EmpresaId, t.Numero }).IsUnique();
            e.HasIndex(t => new { t.TenantId, t.VendaId });
            e.HasOne<Venda>().WithMany().HasForeignKey(t => t.VendaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Devolvidos).WithOne().HasForeignKey(i => i.TrocaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(t => t.Devolvidos).HasField("_devolvidos").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(t => t.Levados).WithOne().HasForeignKey(i => i.TrocaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(t => t.Levados).HasField("_levados").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(t => t.Pagamentos).WithOne().HasForeignKey(p => p.TrocaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(t => t.Pagamentos).HasField("_pagamentos").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemDevolvidoNaTroca>(e =>
        {
            e.ToTable("itens_devolvidos");
            e.Property(i => i.Descricao).HasMaxLength(200);
            e.Property(i => i.Valor).HasPrecision(14, 2);
            e.Property(i => i.CustoUnitario).HasPrecision(14, 4);
            e.Property(i => i.Destino).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ItemLevadoNaTroca>(e =>
        {
            e.ToTable("itens_levados");
            e.Property(i => i.Descricao).HasMaxLength(200);
            e.Property(i => i.PrecoUnitario).HasPrecision(14, 2);
            e.Property(i => i.Desconto).HasPrecision(14, 2);
            e.Property(i => i.CustoUnitario).HasPrecision(14, 4);
            e.Ignore(i => i.Total);
        });

        modelBuilder.Entity<PagamentoDaTroca>(e =>
        {
            e.ToTable("pagamentos_da_troca");
            e.Property(p => p.FormaNome).HasMaxLength(60);
            e.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Valor).HasPrecision(14, 2);
            e.Property(p => p.TaxaPercentual).HasPrecision(5, 2);
            e.Ignore(p => p.ValorTaxa);
        });

        modelBuilder.Entity<ValeTroca>(e =>
        {
            e.ToTable("vales_troca");
            e.Property(v => v.Codigo).HasMaxLength(12);
            e.Property(v => v.ClienteNome).HasMaxLength(150);
            e.Property(v => v.Valor).HasPrecision(14, 2);
            e.Property(v => v.Saldo).HasPrecision(14, 2);
            e.Property(v => v.MotivoDoCancelamento).HasMaxLength(300);
            e.Property(v => v.CanceladoPor).HasMaxLength(150);
            e.HasIndex(v => new { v.TenantId, v.Codigo }).IsUnique();
            e.HasIndex(v => new { v.TenantId, v.ClienteId });
        });

        modelBuilder.Entity<Condicional>(e =>
        {
            e.ToTable("condicionais");
            e.Property(c => c.ClienteNome).HasMaxLength(150);
            e.Property(c => c.VendedorNome).HasMaxLength(150);
            e.Property(c => c.Observacao).HasMaxLength(500);
            e.Property(c => c.FechadaPor).HasMaxLength(150);
            e.Property(c => c.RegistradaPor).HasMaxLength(150);
            e.Ignore(c => c.Pecas);
            e.Ignore(c => c.Valor);
            e.Ignore(c => c.PecasVendidas);
            e.Ignore(c => c.ValorVendido);
            e.HasIndex(c => new { c.TenantId, c.EmpresaId, c.Numero }).IsUnique();
            e.HasIndex(c => new { c.TenantId, c.ClienteId, c.Fechada });
            e.HasMany(c => c.Itens).WithOne().HasForeignKey(i => i.CondicionalId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(c => c.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemDaCondicional>(e =>
        {
            e.ToTable("itens_da_condicional");
            e.Property(i => i.Descricao).HasMaxLength(200);
            e.Property(i => i.PrecoUnitario).HasPrecision(14, 2);
            e.Property(i => i.CustoUnitario).HasPrecision(14, 4);
        });

        modelBuilder.Entity<FechamentoDeComissao>(e =>
        {
            e.ToTable("fechamentos_de_comissao");
            e.Property(f => f.VendedorNome).HasMaxLength(150);
            e.Property(f => f.TotalVendido).HasPrecision(14, 2);
            e.Property(f => f.Comissao).HasPrecision(14, 2);
            e.Property(f => f.FechadoPor).HasMaxLength(150);
            e.HasIndex(f => new { f.TenantId, f.EmpresaId, f.VendedorId });
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
