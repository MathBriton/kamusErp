using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Notificacoes.Aplicacao;
using Kamus.Modules.Notificacoes.Endpoints;
using Kamus.Modules.Notificacoes.Persistencia;
using Kamus.Modules.Notificacoes.TempoReal;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kamus.Modules.Notificacoes;

public static class ModuloNotificacoes
{
    public static IServiceCollection AddModuloNotificacoes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<NotificacoesDbContext>(configuration, NotificacoesDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<INotificador, Notificador>();
        if (configuration.GetValue("Push:Habilitado", true))
            services.AddHttpClient<IEnvioDePush, EnvioDePushExpo>(c => c.Timeout = TimeSpan.FromSeconds(10));
        else
            services.AddSingleton<IEnvioDePush, EnvioDePushDesligado>();
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, UsuarioIdProvider>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloNotificacoes(this WebApplication app)
    {
        app.MapNotificacoes(incluirEndpointDeTeste: app.Environment.IsDevelopment());
        app.MapHub<NotificacoesHub>(NotificacoesHub.Rota);
        return app;
    }
}
