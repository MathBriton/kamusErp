using Kamus.Modules.Notificacoes.Dominio;
using Kamus.Modules.Notificacoes.Persistencia;
using Kamus.Modules.Notificacoes.TempoReal;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Notificacoes.Aplicacao;

public sealed record NotificacaoDto(Guid Id, string Tipo, string Titulo, string Mensagem, string? Link, Guid? EmpresaId, DateTimeOffset CriadaEm, DateTimeOffset? LidaEm)
{
    internal static NotificacaoDto De(Notificacao n) => new(n.Id, n.Tipo, n.Titulo, n.Mensagem, n.Link, n.EmpresaId, n.CriadaEm, n.LidaEm);
}

internal sealed class Notificador(NotificacoesDbContext db, IHubContext<NotificacoesHub> hub, IEnvioDePush push, TimeProvider relogio, ILogger<Notificador> log)
    : INotificador
{
    public async Task NotificarAsync(NovaNotificacao n, CancellationToken ct = default)
    {
        var notificacao = Notificacao.Criar(n.UsuarioId, n.Tipo, n.Titulo, n.Mensagem, n.Link, n.EmpresaId, relogio.GetUtcNow());
        db.Notificacoes.Add(notificacao);
        await db.SaveChangesAsync(ct);

        // Entrega em tempo real para todas as abas/aparelhos conectados desse usuário.
        await hub.Clients.User(n.UsuarioId.ToString()).SendAsync(NotificacoesHub.EventoNotificacao, NotificacaoDto.De(notificacao), ct);
        await EnviarPushAsync(n, ct);
    }

    /// <summary>Push para os aparelhos do usuário. Falha no push não desfaz a notificação (ela já está no sininho).</summary>
    private async Task EnviarPushAsync(NovaNotificacao n, CancellationToken ct)
    {
        var dispositivos = await db.Dispositivos.Where(d => d.UsuarioId == n.UsuarioId).ToListAsync(ct);
        if (dispositivos.Count == 0) return;
        try
        {
            var invalidos = await push.EnviarAsync(new MensagemDePush(dispositivos.Select(d => d.Token).ToList(), n.Titulo, n.Mensagem, n.Link), ct);
            if (invalidos.Count == 0) return;
            db.Dispositivos.RemoveRange(dispositivos.Where(d => invalidos.Contains(d.Token)));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.FalhaAoEnviarPush(ex, n.UsuarioId);
        }
    }
}
