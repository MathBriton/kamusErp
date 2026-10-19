using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Indicadores.Aplicacao;

/// <summary>Roda o <see cref="GeradorDeResumos"/> a cada 5 minutos (o primeiro, 1 minuto depois da subida).</summary>
internal sealed class RotinaDoResumoDiario(IServiceScopeFactory escopos, TimeProvider relogio, IConfiguration configuracao, ILogger<RotinaDoResumoDiario> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracao.GetValue("Indicadores:RotinaAtiva", true)) return;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), relogio, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), relogio);
            do
            {
                try
                {
                    await using var escopo = escopos.CreateAsyncScope();
                    await escopo.ServiceProvider.GetRequiredService<GeradorDeResumos>().ExecutarAsync(relogio.GetUtcNow(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.FalhaNaRotina(ex);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerrando a aplicação.
        }
    }
}
