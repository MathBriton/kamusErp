using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Kamus.IntegrationTests.MultiTenancy;

[Collection(PostgresGroup.Name)]
public sealed class MultiTenancyTests(PostgresFixture db) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public async ValueTask InitializeAsync()
    {
        // Schema criado pelo admin (dono das tabelas); a aplicação usa o papel kamus_app.
        await db.ExecuteAsAdminAsync($"DROP SCHEMA IF EXISTS {TestDbContext.Schema} CASCADE;");
        await using (var admin = CreateContext(null, db.AdminConnectionString))
            await admin.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
        await db.ExecuteAsAdminAsync(TenantRowLevelSecurity.EnableSql(TestDbContext.Schema, "notas"));
        await db.ExecuteAsAdminAsync($"""
            GRANT USAGE ON SCHEMA {TestDbContext.Schema} TO {PostgresFixture.AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {TestDbContext.Schema} TO {PostgresFixture.AppRole};
            """);

        await using var a = CreateContext(TenantA);
        a.Notas.AddRange(new Nota { Texto = "A1" }, new Nota { Texto = "A2" });
        await a.SaveChangesAsync(Ct);

        await using var b = CreateContext(TenantB);
        b.Notas.Add(new Nota { Texto = "B1" });
        await b.SaveChangesAsync(Ct);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Consulta_RetornaSomenteRegistrosDoTenantAtual()
    {
        await using var ctx = CreateContext(TenantA);

        var textos = await ctx.Notas.Select(n => n.Texto).OrderBy(t => t).ToListAsync(Ct);

        textos.ShouldBe(["A1", "A2"]);
    }

    [Fact]
    public async Task Consulta_SemTenantDefinido_NaoRetornaNada()
    {
        await using var ctx = CreateContext(null);

        (await ctx.Notas.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Inclusao_PreencheTenantIdAutomaticamente()
    {
        await using var ctx = CreateContext(TenantB);
        var nota = new Nota { Texto = "B2" };

        ctx.Notas.Add(nota);
        await ctx.SaveChangesAsync(Ct);

        nota.TenantId.ShouldBe(TenantB);
    }

    [Fact]
    public async Task Inclusao_SemTenantDefinido_LancaExcecao()
    {
        await using var ctx = CreateContext(null);
        ctx.Notas.Add(new Nota { Texto = "sem dono" });

        await Should.ThrowAsync<TenantNaoDefinidoException>(() => ctx.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Alteracao_DeRegistroDeOutroTenant_LancaExcecao()
    {
        Nota notaDeB;
        await using (var b = CreateContext(TenantB))
            notaDeB = await b.Notas.AsNoTracking().FirstAsync(Ct);

        await using var a = CreateContext(TenantA);
        a.Notas.Attach(notaDeB);
        notaDeB.Texto = "invadido";

        await Should.ThrowAsync<EscritaEntreTenantsException>(() => a.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Alteracao_DoTenantIdDeUmRegistro_LancaExcecao()
    {
        await using var ctx = CreateContext(TenantA);
        var nota = await ctx.Notas.FirstAsync(Ct);

        ctx.Entry(nota).Property(n => n.TenantId).CurrentValue = TenantB;

        await Should.ThrowAsync<EscritaEntreTenantsException>(() => ctx.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task RowLevelSecurity_MesmoIgnorandoFiltroDoEf_BancoSoEntregaDadosDoTenant()
    {
        await using var ctx = CreateContext(TenantA);

        var total = await ctx.Notas.IgnoreQueryFilters().CountAsync(Ct);

        total.ShouldBe(2);
    }

    [Fact]
    public async Task RowLevelSecurity_InsercaoDiretaComOutroTenant_EhRejeitadaPeloBanco()
    {
        await using var ctx = CreateContext(TenantA);

        var ex = await Should.ThrowAsync<PostgresException>(() => ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO testes.notas (id, tenant_id, texto) VALUES ({Guid.NewGuid()}, {TenantB}, 'B via SQL')", Ct));

        ex.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    private TestDbContext CreateContext(Guid? tenantId, string? connectionString = null)
    {
        var tenant = new TenantContext();
        if (tenantId is { } id) tenant.Definir(id);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseKamusPostgres(connectionString ?? db.AppConnectionString, tenant)
            .Options;
        return new TestDbContext(options, tenant);
    }
}
