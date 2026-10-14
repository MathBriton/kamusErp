using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Notificacoes.Dominio;

/// <summary>Aviso para um usuário (sininho). Não entra no histórico de alterações: é volumoso e não é dado de negócio.</summary>
[NaoAuditar]
public sealed class Notificacao : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? EmpresaId { get; private set; }
    public string Tipo { get; private set; } = "";
    public string Titulo { get; private set; } = "";
    public string Mensagem { get; private set; } = "";
    public string? Link { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? LidaEm { get; private set; }

    private Notificacao() { }

    public static Notificacao Criar(Guid usuarioId, string tipo, string titulo, string mensagem, string? link, Guid? empresaId, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        return new Notificacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            EmpresaId = empresaId,
            Tipo = tipo,
            Titulo = titulo.Trim(),
            Mensagem = mensagem.Trim(),
            Link = link,
            CriadaEm = agora,
        };
    }

    public void MarcarComoLida(DateTimeOffset agora) => LidaEm ??= agora;
}
