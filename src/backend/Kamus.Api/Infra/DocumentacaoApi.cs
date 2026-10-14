using Scalar.AspNetCore;

namespace Kamus.Api.Infra;

/// <summary>OpenAPI + tela de testes (Scalar) — somente em Development.</summary>
internal static class DocumentacaoApi
{
    public static IServiceCollection AddKamusDocumentacaoApi(this IServiceCollection services) => services.AddOpenApi();

    public static WebApplication MapKamusDocumentacaoApi(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;

        app.MapOpenApi();                                    // /openapi/v1.json
        app.MapScalarApiReference(o => o.WithTitle("Kamus API")); // /scalar
        return app;
    }
}
