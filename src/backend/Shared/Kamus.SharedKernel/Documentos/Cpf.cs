using System.Diagnostics.CodeAnalysis;

namespace Kamus.SharedKernel.Documentos;

/// <summary>CPF (11 dígitos, módulo 11). Aceita com ou sem máscara.</summary>
public sealed record Cpf
{
    private const int Tamanho = 11;

    /// <summary>11 dígitos, sem máscara.</summary>
    public string Valor { get; }

    public string Formatado => $"{Valor[..3]}.{Valor[3..6]}.{Valor[6..9]}-{Valor[9..]}";

    private Cpf(string valor) => Valor = valor;

    public static Cpf Parse(string? entrada) =>
        TryParse(entrada, out var cpf) ? cpf : throw new FormatException("CPF inválido.");

    public static bool TryParse(string? entrada, [NotNullWhen(true)] out Cpf? cpf)
    {
        cpf = null;
        if (string.IsNullOrWhiteSpace(entrada)) return false;

        Span<char> d = stackalloc char[Tamanho];
        var n = 0;
        foreach (var c in entrada)
        {
            if (c is '.' or '-' || char.IsWhiteSpace(c)) continue;
            if (!char.IsAsciiDigit(c) || n == Tamanho) return false;
            d[n++] = c;
        }
        // Sequências repetidas (111.111.111-11...) passam no cálculo, mas não são CPFs reais.
        if (n != Tamanho || d.IndexOfAnyExcept(d[0]) < 0) return false;
        if (Dv(d[..9]) != d[9] - '0' || Dv(d[..10]) != d[10] - '0') return false;

        cpf = new Cpf(new string(d));
        return true;
    }

    public override string ToString() => Formatado;

    private static int Dv(ReadOnlySpan<char> base_)
    {
        var soma = 0;
        for (var i = 0; i < base_.Length; i++)
            soma += (base_[i] - '0') * (base_.Length + 1 - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
