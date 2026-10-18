using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Financeiro.Dominio;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kamus.Modules.Financeiro.Persistencia;

public sealed class FinanceiroDbContext(DbContextOptions<FinanceiroDbContext> options, ITenantContext tenant)
    : KamusDbContext(options, tenant)
{
    public const string Schema = "financeiro";

    public DbSet<ContaFinanceira> Contas => Set<ContaFinanceira>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Titulo> Titulos => Set<Titulo>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();
    public DbSet<Baixa> Baixas => Set<Baixa>();
    public DbSet<MovimentoFinanceiro> Movimentos => Set<MovimentoFinanceiro>();
    public DbSet<LimiteDeCredito> LimitesDeCredito => Set<LimiteDeCredito>();
    public DbSet<ConfiguracaoDoCrediario> ConfiguracoesDoCrediario => Set<ConfiguracaoDoCrediario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ContaFinanceira>(e =>
        {
            e.ToTable("contas");
            e.Property(c => c.Nome).HasMaxLength(ContaFinanceira.TamanhoMaximoDoNome);
            e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.SaldoInicial).HasPrecision(14, 2);
            e.HasIndex(c => new { c.TenantId, c.EmpresaId, c.Nome }).IsUnique();
        });

        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("categorias");
            e.Property(c => c.Nome).HasMaxLength(60);
            e.Property(c => c.Grupo).HasMaxLength(60);
            e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => new { c.TenantId, c.Tipo, c.Nome }).IsUnique();
        });

        modelBuilder.Entity<Titulo>(e =>
        {
            e.ToTable("titulos");
            e.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Descricao).HasMaxLength(150);
            e.Property(t => t.PessoaNome).HasMaxLength(150);
            e.Property(t => t.Documento).HasMaxLength(60);
            e.Property(t => t.Observacao).HasMaxLength(1000);
            e.Property(t => t.Origem).HasMaxLength(30);
            e.Property(t => t.OrigemId).HasMaxLength(60);
            e.Ignore(t => t.ValorTotal);
            e.Ignore(t => t.Quitado);
            e.HasIndex(t => new { t.TenantId, t.EmpresaId, t.Tipo });
            e.HasIndex(t => new { t.TenantId, t.Origem, t.OrigemId });
            e.HasIndex(t => new { t.TenantId, t.PessoaId, t.Crediario });
            e.HasOne<Categoria>().WithMany().HasForeignKey(t => t.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Parcelas).WithOne().HasForeignKey(p => p.TituloId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(t => t.Parcelas).HasField("_parcelas").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Parcela>(e =>
        {
            e.ToTable("parcelas");
            e.Property(p => p.Valor).HasPrecision(14, 2);
            e.Ignore(p => p.ValorPago);
            e.Ignore(p => p.Saldo);
            e.HasIndex(p => new { p.TenantId, p.Vencimento });
            e.HasMany(p => p.Baixas).WithOne().HasForeignKey(b => b.ParcelaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(p => p.Baixas).HasField("_baixas").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Baixa>(e =>
        {
            e.ToTable("baixas");
            e.Property(b => b.Valor).HasPrecision(14, 2);
            e.Property(b => b.Juros).HasPrecision(14, 2);
            e.Property(b => b.Multa).HasPrecision(14, 2);
            e.Property(b => b.Desconto).HasPrecision(14, 2);
            e.Property(b => b.Usuario).HasMaxLength(150);
            e.Property(b => b.EstornadaPor).HasMaxLength(150);
            e.Ignore(b => b.Total);
            e.HasOne<ContaFinanceira>().WithMany().HasForeignKey(b => b.ContaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentoFinanceiro>(e =>
        {
            e.ToTable("movimentos");
            e.Property(m => m.Valor).HasPrecision(14, 2);
            e.Property(m => m.Descricao).HasMaxLength(200);
            e.Property(m => m.Origem).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Usuario).HasMaxLength(150);
            e.HasIndex(m => new { m.TenantId, m.ContaId, m.Data });
            e.HasOne<ContaFinanceira>().WithMany().HasForeignKey(m => m.ContaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LimiteDeCredito>(e =>
        {
            e.ToTable("limites_de_credito");
            e.Property(l => l.ClienteNome).HasMaxLength(150);
            e.Property(l => l.Limite).HasPrecision(14, 2);
            e.Property(l => l.Observacao).HasMaxLength(300);
            e.Property(l => l.AtualizadoPor).HasMaxLength(150);
            e.HasIndex(l => new { l.TenantId, l.ClienteId }).IsUnique();
        });

        modelBuilder.Entity<ConfiguracaoDoCrediario>(e =>
        {
            e.ToTable("configuracoes_do_crediario");
            e.Property(c => c.MultaPercentual).HasPrecision(5, 2);
            e.Property(c => c.JurosMensalPercentual).HasPrecision(5, 2);
            e.HasIndex(c => c.TenantId).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c> (não conecta no banco).</summary>
internal sealed class FinanceiroDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceiroDbContext>
{
    public FinanceiroDbContext CreateDbContext(string[] args)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<FinanceiroDbContext>()
            .UseKamusPostgres("Host=localhost;Database=kamus_design", tenant, FinanceiroDbContext.Schema)
            .Options;
        return new FinanceiroDbContext(options, tenant);
    }
}
