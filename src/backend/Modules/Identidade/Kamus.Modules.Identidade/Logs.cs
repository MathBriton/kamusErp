using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Identidade;

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Sem ConnectionStrings:Kamus — dados em MEMÓRIA: tudo é perdido ao reiniciar a API.")]
    public static partial void BancoEmMemoria(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations do módulo Identidade aplicadas.")]
    public static partial void MigrationsAplicadas(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dados de demonstração prontos: login {Email}")]
    public static partial void DemonstracaoPronta(this ILogger logger, string email);
}
