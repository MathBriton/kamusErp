using System.ComponentModel;
using System.Net.Mail;
using Kamus.SharedKernel.Auditoria;

namespace Kamus.Modules.Identidade.Dominio;

/// <summary>
/// Usuário que faz login. Não implementa <c>ITenantOwned</c> de propósito: no login o tenant ainda
/// não é conhecido, então a busca por e-mail precisa enxergar todos. O <see cref="TenantId"/> é
/// gravado e define o tenant da sessão após a senha ser validada. E-mail é único no sistema.
/// </summary>
[Auditavel]
[DisplayName("Usuário")]
public sealed class Usuario
{
    public const int MaximoDeFalhas = 5;
    public static readonly TimeSpan TempoDeBloqueio = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = "";
    public string Email { get; private set; } = "";
    [NaoAuditar] public string SenhaHash { get; private set; } = "";
    public bool Ativo { get; private set; }
    // Contadores técnicos do login: viram eventos no histórico (login, falha, bloqueio), não alterações.
    [NaoAuditar] public int FalhasDeLogin { get; private set; }
    [NaoAuditar] public DateTimeOffset? BloqueadoAte { get; private set; }
    [NaoAuditar] public DateTimeOffset? UltimoLoginEm { get; private set; }

    /// <summary>Senha provisória (criada por outra pessoa): só pode usar o sistema depois de trocar.</summary>
    public bool DeveTrocarSenha { get; private set; }

    private Usuario() { }

    public static Usuario Criar(Guid tenantId, string nome, string email, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new Usuario
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome.Trim(),
            Email = NormalizarEmail(email) ?? throw new ArgumentException("E-mail inválido.", nameof(email)),
            Ativo = true,
        };
    }

    /// <summary>E-mail em minúsculas e sem espaços, ou <c>null</c> se não for um e-mail válido.</summary>
    public static string? NormalizarEmail(string? email)
    {
        var limpo = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(limpo) || !MailAddress.TryCreate(limpo, out var endereco) || endereco.Address != limpo)
            return null;
        return limpo;
    }

    public void DefinirSenhaHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        SenhaHash = hash;
    }

    public void DefinirSenhaProvisoria(string hash)
    {
        DefinirSenhaHash(hash);
        DeveTrocarSenha = true;
    }

    public void TrocarSenha(string hash)
    {
        DefinirSenhaHash(hash);
        DeveTrocarSenha = false;
    }

    public void Renomear(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        Nome = nome.Trim();
    }

    public void Desativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    public bool EstaBloqueado(DateTimeOffset agora) => BloqueadoAte is { } ate && agora < ate;

    public void RegistrarFalhaDeLogin(DateTimeOffset agora)
    {
        if (BloqueadoAte is { } ate && agora >= ate)
        {
            BloqueadoAte = null;
            FalhasDeLogin = 0;
        }

        FalhasDeLogin++;
        if (FalhasDeLogin >= MaximoDeFalhas)
        {
            BloqueadoAte = agora + TempoDeBloqueio;
            FalhasDeLogin = 0;
        }
    }

    public void RegistrarLoginComSucesso(DateTimeOffset agora)
    {
        FalhasDeLogin = 0;
        BloqueadoAte = null;
        UltimoLoginEm = agora;
    }
}
