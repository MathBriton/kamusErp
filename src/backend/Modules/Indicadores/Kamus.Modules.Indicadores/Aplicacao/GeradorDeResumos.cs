using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Indicadores.Dominio;
using Kamus.Modules.Indicadores.Persistencia;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Indicadores.Aplicacao;

/// <summary>
/// Envia o resumo diário no sininho de quem chegou ao horário escolhido (Brasília). Cada usuário recebe no máximo um por dia
/// (fica marcado nas preferências), então pode rodar quantas vezes quiser. Sem movimento, só envia se o usuário pediu.
/// </summary>
public sealed class GeradorDeResumos(IDiretorioDeClientes diretorio, IServiceScopeFactory escopos, ILogger<GeradorDeResumos> log)
{
    public const string Tipo = "resumo_diario";

    public async Task ExecutarAsync(DateTimeOffset agora, CancellationToken ct = default)
    {
        foreach (var cliente in await diretorio.ListarAsync(ct))
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(cliente.TenantId);
                await ExecutarNoTenantAsync(escopo.ServiceProvider, agora, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.FalhaNoResumo(ex, cliente.TenantId);
            }
        }
    }

    private static async Task ExecutarNoTenantAsync(IServiceProvider sp, DateTimeOffset agora, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IndicadoresDbContext>();
        var acessos = (await sp.GetRequiredService<IDiretorioDeClientes>().AcessosDoTenantAtualAsync(ct)).ToLookup(a => a.UsuarioId);
        var preferencias = await db.Preferencias.ToDictionaryAsync(p => p.UsuarioId, ct);
        var resumo = sp.GetRequiredService<ResumoDiario>();
        var notificador = sp.GetRequiredService<INotificador>();
        var local = agora.ToOffset(TimeSpan.FromHours(-3)).DateTime;
        var hoje = DateOnly.FromDateTime(local);

        foreach (var usuario in acessos)
        {
            if (!preferencias.TryGetValue(usuario.Key, out var p))
            {
                var dono = usuario.Any(a => a.Perfil == "Dono");
                if (!dono) continue;
                p = PreferenciasDoResumo.Padrao(usuario.Key, dono);
                db.Preferencias.Add(p);
            }
            if (!p.DeveEnviar(local)) continue;

            var previa = await resumo.MontarAsync(usuario.Key, usuario.ToList(), p, hoje, ct);
            if (previa.TemMovimento || p.EnviarSemMovimento)
                await notificador.NotificarAsync(new NovaNotificacao(usuario.Key, Tipo, previa.Titulo, previa.Mensagem(), "/"), ct);
            p.MarcarEnvio(hoje);
            await db.SaveChangesAsync(ct);
        }
    }
}
