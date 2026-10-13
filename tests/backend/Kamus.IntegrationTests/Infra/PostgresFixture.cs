using Npgsql;
using Testcontainers.PostgreSql;

namespace Kamus.IntegrationTests.Infra;

/// <summary>
/// Um PostgreSQL real por execução de testes. Além do superusuário do container,
/// cria o papel <c>kamus_app</c> (sem superusuário), que é como a aplicação se conecta
/// em produção — necessário porque superusuários ignoram Row Level Security.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppRole = "kamus_app";
    public const string AppPassword = "kamus_app_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public string AppConnectionString =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = AppRole, Password = AppPassword }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteAsAdminAsync($"CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOBYPASSRLS;");
    }

    public async Task ExecuteAsAdminAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class PostgresGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
