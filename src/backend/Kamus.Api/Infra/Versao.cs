using System.Reflection;

namespace Kamus.Api.Infra;

internal static class Versao
{
    internal sealed record VersaoResponse(string Versao, string? Commit);

    // Ex.: "0.2.0+3f2c1ab..." → versão "0.2.0", commit "3f2c1ab..."
    private static readonly VersaoResponse Atual = Ler();

    public static IEndpointRouteBuilder MapVersao(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/versao", () => Atual);
        return app;
    }

    private static VersaoResponse Ler()
    {
        var informacional = typeof(Versao).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var partes = informacional.Split('+', 2);
        return new VersaoResponse(partes[0], partes.Length > 1 ? partes[1] : null);
    }
}
