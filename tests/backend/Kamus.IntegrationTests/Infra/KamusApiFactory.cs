using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kamus.IntegrationTests.Infra;

/// <param name="connectionString"><c>null</c> = API sem banco configurado (dados em memória).</param>
/// <param name="configuracoes">Configurações extras (chave no formato "Secao:Chave").</param>
public sealed class KamusApiFactory(
    string? connectionString = null,
    string ambiente = "Testing",
    IReadOnlyDictionary<string, string>? configuracoes = null) : WebApplicationFactory<Program>
{
    public const string ChaveJwtDeTeste = "chave-de-teste-com-pelo-menos-32-caracteres!!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ambiente);
        // Sempre sobrescreve: em Development o appsettings traz valores locais.
        builder.UseSetting("ConnectionStrings:Kamus", connectionString ?? "");
        builder.UseSetting("Jwt:Chave", ChaveJwtDeTeste);
        foreach (var (chave, valor) in configuracoes ?? new Dictionary<string, string>())
            builder.UseSetting(chave, valor);
    }
}
