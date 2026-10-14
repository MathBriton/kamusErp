using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kamus.Modules.Notificacoes.TempoReal;

/// <summary>Canal em tempo real do navegador/app. O cliente só recebe: <c>notificacao</c>.</summary>
[Authorize]
internal sealed class NotificacoesHub : Hub
{
    public const string Rota = "/hubs/notificacoes";
    public const string EventoNotificacao = "notificacao";
}

/// <summary>Identifica a conexão pelo claim <c>sub</c> do JWT (id do usuário).</summary>
internal sealed class UsuarioIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst("sub")?.Value;
}
