using System.ComponentModel;
using System.Net.Mail;
using Kamus.SharedKernel.Auditoria;

namespace Kamus.Modules.Licenciamento.Dominio;

/// <summary>Operador do SaaS (SuperAdmin) que acessa o Painel Administrativo. Não pertence a nenhum cliente.</summary>
[DisplayName("Administrador")]
[NaoAuditar]
public sealed class Administrador
{
    public const int MaximoDeFalhas = 5;
    public static readonly TimeSpan TempoDeBloqueio = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string SenhaHash { get; private set; } = "";
    public bool Ativo { get; private set; }
    public int FalhasDeLogin { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }
    public DateTimeOffset? UltimoLoginEm { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Administrador() { }

    public static Administrador Criar(string nome, string email, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new Administrador
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Email = NormalizarEmail(email) ?? throw new ArgumentException("E-mail inválido.", nameof(email)),
            Ativo = true,
            CriadoEm = agora,
        };
    }

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
