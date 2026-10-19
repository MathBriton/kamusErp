using System.Text.RegularExpressions;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Notificacoes.Dominio;

/// <summary>Aparelho com o app mobile instalado: recebe por push as notificações do usuário. Dado técnico, fora do histórico.</summary>
[NaoAuditar]
public sealed partial class Dispositivo : ITenantOwned
{
    public const int TamanhoMaximoDoToken = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UsuarioId { get; private set; }

    /// <summary>Token do Expo Push (ex.: <c>ExponentPushToken[...]</c>).</summary>
    public string Token { get; private set; } = "";

    public string Plataforma { get; private set; } = "";
    public DateTimeOffset RegistradoEm { get; private set; }

    private Dispositivo() { }

    [GeneratedRegex(@"^Expo(nent)?PushToken\[[^\]]+\]$")]
    private static partial Regex FormatoDoToken();

    public static bool TokenValido(string? token) => token is { Length: <= TamanhoMaximoDoToken } && FormatoDoToken().IsMatch(token);

    public static Dispositivo Registrar(Guid usuarioId, string token, string? plataforma, DateTimeOffset agora)
    {
        if (!TokenValido(token)) throw new ArgumentException("Token de push inválido.", nameof(token));
        return new Dispositivo { Id = Guid.NewGuid(), UsuarioId = usuarioId, Token = token, Plataforma = NormalizarPlataforma(plataforma), RegistradoEm = agora };
    }

    /// <summary>O mesmo aparelho passou para outro usuário (outro login no app).</summary>
    public void Transferir(Guid usuarioId, string? plataforma, DateTimeOffset agora) => (UsuarioId, Plataforma, RegistradoEm) = (usuarioId, NormalizarPlataforma(plataforma), agora);

    private static string NormalizarPlataforma(string? p) => p?.Trim().ToLowerInvariant() is { Length: > 0 and <= 20 } valida ? valida : "desconhecida";
}
