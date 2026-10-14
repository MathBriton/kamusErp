using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Cadastros;

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Falha na importação em segundo plano ({Tipo}) do cliente {TenantId}")]
    public static partial void FalhaNaImportacao(this ILogger logger, Exception ex, string tipo, Guid tenantId);
}
