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
    public DbSet<NotaDeEntrada> Notas => Set<NotaDeEntrada>();
    public DbSet<ItemDaNota> ItensDeNota => Set<ItemDaNota>();
    public DbSet<DistribuicaoDoItem> Distribuicoes => Set<DistribuicaoDoItem>();
    public DbSet<VinculoDeProduto> Vinculos => Set<VinculoDeProduto>();

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

        modelBuilder.Entity<NotaDeEntrada>(e =>
        {
            e.ToTable("notas");
            e.Property(n => n.Chave).HasMaxLength(44);
            e.Property(n => n.Numero).HasMaxLength(9);
            e.Property(n => n.Serie).HasMaxLength(3);
            e.Property(n => n.FornecedorDocumento).HasMaxLength(14);
            e.Property(n => n.FornecedorNome).HasMaxLength(150);
            e.Property(n => n.ValorTotal).HasPrecision(14, 2);
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(n => n.ImportadaPor).HasMaxLength(150);
            e.Property(n => n.LancadaPor).HasMaxLength(150);
            e.Ignore(n => n.ProntaParaLancar);
            e.HasIndex(n => new { n.TenantId, n.Chave });
            e.HasIndex(n => new { n.TenantId, n.EmpresaId, n.ImportadaEm });
            e.HasMany(n => n.Itens).WithOne().HasForeignKey(i => i.NotaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(n => n.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(n => n.Duplicatas).WithOne().HasForeignKey(d => d.NotaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(n => n.Duplicatas).HasField("_duplicatas").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemDaNota>(e =>
        {
            e.ToTable("itens_de_nota");
            e.Property(i => i.Codigo).HasMaxLength(60);
            e.Property(i => i.Ean).HasMaxLength(14);
            e.Property(i => i.Descricao).HasMaxLength(120);
            e.Property(i => i.Ncm).HasMaxLength(8);
            e.Property(i => i.Unidade).HasMaxLength(6);
            e.Property(i => i.Quantidade).HasPrecision(15, 4);
            e.Property(i => i.ValorTotal).HasPrecision(14, 2);
            e.Property(i => i.CustoTotal).HasPrecision(14, 2);
            e.Property(i => i.OrigemDoVinculo).HasMaxLength(20);
            e.Ignore(i => i.PecasEsperadas);
            e.Ignore(i => i.Situacao);
            e.Ignore(i => i.CustoPorPeca);
            e.HasIndex(i => new { i.NotaId, i.Numero }).IsUnique();
            e.HasMany(i => i.Distribuicao).WithOne().HasForeignKey(d => d.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(i => i.Distribuicao).HasField("_distribuicao").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DistribuicaoDoItem>(e => e.ToTable("distribuicoes_de_item"));

        modelBuilder.Entity<DuplicataDaNota>(e =>
        {
            e.ToTable("duplicatas_de_nota");
            e.Property(d => d.Numero).HasMaxLength(60);
            e.Property(d => d.Valor).HasPrecision(14, 2);
        });

        modelBuilder.Entity<VinculoDeProduto>(e =>
        {
            e.ToTable("vinculos_de_produto");
            e.Property(v => v.FornecedorDocumento).HasMaxLength(14);
            e.Property(v => v.Codigo).HasMaxLength(60);
            e.HasIndex(v => new { v.TenantId, v.FornecedorDocumento, v.Codigo }).IsUnique();
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
