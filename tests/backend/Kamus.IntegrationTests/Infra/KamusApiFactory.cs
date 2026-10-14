using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kamus.IntegrationTests.Infra;

/// <param name="connectionString"><c>null</c> = API sem banco configurado (modo local sem PostgreSQL).</param>
public sealed class KamusApiFactory(string? connectionString = null, string ambiente = "Testing") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ambiente);
        // Sempre sobrescreve: em Development o appsettings traz uma connection string local.
        builder.UseSetting("ConnectionStrings:Kamus", connectionString ?? "");
    }
}
