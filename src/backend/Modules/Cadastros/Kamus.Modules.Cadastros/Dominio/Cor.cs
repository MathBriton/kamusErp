using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Cadastros.Dominio;

[DisplayName("Cor")]
public sealed class Cor : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = "";

    /// <summary>Amostra para a tela (#RRGGBB), opcional.</summary>
    public string? Hex { get; private set; }

    public static readonly IReadOnlyList<(string Nome, string Hex)> Padroes =
    [
        ("Preto", "#111827"), ("Branco", "#FFFFFF"), ("Cinza", "#9CA3AF"), ("Azul", "#1D4ED8"), ("Azul marinho", "#1E3A5F"),
        ("Vermelho", "#DC2626"), ("Rosa", "#EC4899"), ("Verde", "#16A34A"), ("Amarelo", "#FACC15"), ("Bege", "#D6C3A5"),
        ("Marrom", "#7C4A2D"), ("Jeans", "#4A6FA5"),
    ];

    private Cor() { }

    public static Cor Criar(string nome, string? hex)
    {
        var cor = new Cor { Id = Guid.NewGuid() };
        cor.Alterar(nome, hex);
        return cor;
    }

    public void Alterar(string nome, string? hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        var h = string.IsNullOrWhiteSpace(hex) ? null : hex.Trim().ToUpperInvariant();
        if (h is not null && (h.Length != 7 || h[0] != '#' || !h[1..].All(char.IsAsciiHexDigit)))
            throw new ArgumentException("Cor inválida: use o formato #RRGGBB.", nameof(hex));
        (Nome, Hex) = (nome.Trim(), h);
    }
}
