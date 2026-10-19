using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Indicadores;

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gerar o resumo diário do cliente {TenantId}")]
    public static partial void FalhaNoResumo(this ILogger logger, Exception ex, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha na rotina do resumo diário")]
    public static partial void FalhaNaRotina(this ILogger logger, Exception ex);
}
