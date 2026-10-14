using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Cadastros.Dominio;

/// <summary>Grade de tamanhos reutilizável (ex.: PP ao GG). A ordem dos tamanhos é a ordem de exibição.</summary>
[DisplayName("Grade de tamanhos")]
public sealed class GradeDeTamanhos : ITenantOwned
{
    public const int MaximoDeTamanhos = 30;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = "";
    public IReadOnlyList<string> Tamanhos { get; private set; } = [];

    public static readonly IReadOnlyList<(string Nome, string[] Tamanhos)> Padroes =
    [
        ("PP ao GG", ["PP", "P", "M", "G", "GG"]),
        ("Plus G1 ao G4", ["G1", "G2", "G3", "G4"]),
        ("Numeração 34 ao 48", ["34", "36", "38", "40", "42", "44", "46", "48"]),
        ("Infantil 1 ao 16", ["1", "2", "3", "4", "6", "8", "10", "12", "14", "16"]),
        ("Calçados 33 ao 44", ["33", "34", "35", "36", "37", "38", "39", "40", "41", "42", "43", "44"]),
        ("Único", ["Único"]),
    ];

    private GradeDeTamanhos() { }

    public static GradeDeTamanhos Criar(string nome, IEnumerable<string> tamanhos)
    {
        var grade = new GradeDeTamanhos { Id = Guid.NewGuid() };
        grade.Alterar(nome, tamanhos);
        return grade;
    }

    public void Alterar(string nome, IEnumerable<string> tamanhos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        var lista = tamanhos.Select(t => t?.Trim() ?? "").ToList();
        if (lista.Count == 0 || lista.Count > MaximoDeTamanhos)
            throw new ArgumentException($"Informe de 1 a {MaximoDeTamanhos} tamanhos.", nameof(tamanhos));
        if (lista.Any(t => t.Length is 0 or > 10))
            throw new ArgumentException("Cada tamanho deve ter de 1 a 10 caracteres.", nameof(tamanhos));
        if (lista.Distinct(StringComparer.OrdinalIgnoreCase).Count() != lista.Count)
            throw new ArgumentException("Há tamanhos repetidos.", nameof(tamanhos));
        (Nome, Tamanhos) = (nome.Trim(), lista);
    }
}
