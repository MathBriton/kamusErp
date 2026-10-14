using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kamus.IntegrationTests.Infra;

/// <summary>
/// Um PostgreSQL real por execução de testes. Além do superusuário do container,
/// cria o papel <c>kamus_app</c> (sem superusuário), que é como a aplicação se conecta
/// em produção — necessário porque superusuários ignoram Row Level Security.
/// <para>
/// Sem Docker na máquina, os testes que dependem do banco são <b>pulados com aviso</b>
/// (desenvolvimento em localhost). No CI (variável <c>CI=true</c>) a falta de Docker é erro.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppRole = "kamus_app";
    public const string AppPassword = "kamus_app_test";

    private PostgreSqlContainer? _container;
    private string? _motivoIndisponivel;

    public string AdminConnectionString => Container.GetConnectionString();

    public string AppConnectionString =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = AppRole, Password = AppPassword }.ConnectionString;

    private PostgreSqlContainer Container => _container ?? throw new InvalidOperationException(_motivoIndisponivel);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex) when (!EmCi && ex is DockerUnavailableException or ArgumentException or InvalidOperationException)
        {
            // Testcontainers lança ao não encontrar um Docker utilizável.
            _container = null;
            _motivoIndisponivel = $"Docker indisponível nesta máquina ({ex.GetType().Name}). Testes com PostgreSQL pulados — eles rodam no CI.";
            return;
        }

        await ExecuteAsAdminAsync($"CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOBYPASSRLS;");
    }

    /// <summary>Chame no início de cada teste (ou do seu InitializeAsync) que precisa do banco.</summary>
    public void ExigirBanco() => Assert.SkipWhen(_container is null, _motivoIndisponivel ?? "");

    public async Task ExecuteAsAdminAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private static bool EmCi => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}

[CollectionDefinition(Name)]
public sealed class PostgresGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
