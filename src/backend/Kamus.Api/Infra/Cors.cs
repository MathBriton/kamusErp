namespace Kamus.Api.Infra;

/// <summary>
/// CORS só para as origens de <c>Cors:Origens</c> (em Development: o Expo web do app mobile, http://localhost:8081).
/// A web e o painel usam o proxy do Vite (mesma origem) e o app nativo não precisa de CORS. Sem origens configuradas, nada é liberado.
/// </summary>
internal static class Cors
{
    private const string Politica = "kamus";

    public static IServiceCollection AddKamusCors(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy(Politica, p => p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }

    public static IApplicationBuilder UseKamusCors(this IApplicationBuilder app) => app.UseCors(Politica);
}
