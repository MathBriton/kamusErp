namespace Kamus.Modules.Identidade.Dominio;

/// <summary>
/// Sessão renovável. Só o hash do token é guardado. A cada renovação o token é trocado (rotação);
/// reapresentar um token já trocado indica roubo e revoga todas as sessões do usuário.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public bool ManterConectado { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }
    public Guid? SubstituidoPorId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Criar(Guid usuarioId, string tokenHash, bool manterConectado, DateTimeOffset agora, TimeSpan validade) =>
        new()
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            ManterConectado = manterConectado,
            CriadoEm = agora,
            ExpiraEm = agora + validade,
        };

    public bool Revogado => RevogadoEm is not null;

    public bool Expirado(DateTimeOffset agora) => agora >= ExpiraEm;

    public void Revogar(DateTimeOffset agora, Guid? substituidoPor = null)
    {
        RevogadoEm ??= agora;
        SubstituidoPorId ??= substituidoPor;
    }
}
