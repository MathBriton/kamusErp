using Kamus.Modules.Notificacoes.Dominio;
using Kamus.Modules.Notificacoes.Persistencia;
using Kamus.Modules.Notificacoes.TempoReal;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.AspNetCore.SignalR;

namespace Kamus.Modules.Notificacoes.Aplicacao;

public sealed record NotificacaoDto(Guid Id, string Tipo, string Titulo, string Mensagem, string? Link, Guid? EmpresaId, DateTimeOffset CriadaEm, DateTimeOffset? LidaEm)
{
    internal static NotificacaoDto De(Notificacao n) => new(n.Id, n.Tipo, n.Titulo, n.Mensagem, n.Link, n.EmpresaId, n.CriadaEm, n.LidaEm);
}

internal sealed class Notificador(NotificacoesDbContext db, IHubContext<NotificacoesHub> hub, TimeProvider relogio) : INotificador
{
    public async Task NotificarAsync(NovaNotificacao n, CancellationToken ct = default)
    {
        var notificacao = Notificacao.Criar(n.UsuarioId, n.Tipo, n.Titulo, n.Mensagem, n.Link, n.EmpresaId, relogio.GetUtcNow());
        db.Notificacoes.Add(notificacao);
        await db.SaveChangesAsync(ct);

        // Entrega em tempo real para todas as abas/aparelhos conectados desse usuário.
        await hub.Clients.User(n.UsuarioId.ToString()).SendAsync(NotificacoesHub.EventoNotificacao, NotificacaoDto.De(notificacao), ct);
    }
}
