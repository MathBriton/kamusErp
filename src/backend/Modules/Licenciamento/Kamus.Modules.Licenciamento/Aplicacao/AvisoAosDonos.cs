using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>Notifica os donos de um cliente a partir de um contexto sem tenant (rotina, Painel Administrativo).</summary>
public sealed class AvisoAosDonos(IServiceScopeFactory escopos)
{
    public const string Link = "/configuracoes/licenca";

    public async Task EnviarAsync(Guid tenantId, string tipo, string titulo, string mensagem, CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        sp.GetRequiredService<TenantContext>().Definir(tenantId);

        var notificador = sp.GetRequiredService<INotificador>();
        foreach (var dono in await sp.GetRequiredService<IDiretorioDeClientes>().DonosDoTenantAtualAsync(ct))
            await notificador.NotificarAsync(new NovaNotificacao(dono, tipo, titulo, mensagem, Link), ct);
    }
}
