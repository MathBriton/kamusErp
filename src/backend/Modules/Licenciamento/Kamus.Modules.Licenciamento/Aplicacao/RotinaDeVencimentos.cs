using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>Roda o <see cref="VerificadorDeVencimentos"/> a cada hora (o primeiro, 1 minuto depois da subida).</summary>
internal sealed class RotinaDeVencimentos(IServiceScopeFactory escopos, TimeProvider relogio, IConfiguration configuracao, ILogger<RotinaDeVencimentos> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracao.GetValue("Licenciamento:RotinaAtiva", true)) return;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), relogio, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1), relogio);
            do
            {
                try
                {
                    await using var escopo = escopos.CreateAsyncScope();
                    await escopo.ServiceProvider.GetRequiredService<VerificadorDeVencimentos>()
                        .ExecutarAsync(CalendarioBrasilia.Hoje(relogio), stoppingToken);
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
