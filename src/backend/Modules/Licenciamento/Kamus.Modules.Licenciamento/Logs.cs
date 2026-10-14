using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Licenciamento;

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao avisar o cliente {TenantId} sobre a licença")]
    public static partial void FalhaAoAvisar(this ILogger logger, Exception ex, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha na rotina de vencimentos de licença")]
    public static partial void FalhaNaRotina(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Licença de demonstração criada para o cliente {TenantId}")]
    public static partial void LicencaDeDemonstracao(this ILogger logger, Guid tenantId);
}
