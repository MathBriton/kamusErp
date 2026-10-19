using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Notificacoes;

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "O serviço de push respondeu {Status}")]
    public static partial void FalhaNoPush(this ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar push para o usuário {UsuarioId}")]
    public static partial void FalhaAoEnviarPush(this ILogger logger, Exception ex, Guid usuarioId);
}
