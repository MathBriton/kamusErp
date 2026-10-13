using System.Diagnostics.CodeAnalysis;

namespace Kamus.SharedKernel.Documentos;

/// <summary>
/// CNPJ numérico ou alfanumérico (IN RFB 2.229/2024, vigente desde julho/2026):
/// as 12 primeiras posições aceitam 0-9 e A-Z; os 2 dígitos verificadores são sempre numéricos.
/// O cálculo do DV usa o valor ASCII do caractere menos 48, com os pesos tradicionais (módulo 11).
/// </summary>
public sealed record Cnpj
{
    private const int Tamanho = 14;
    private static readonly int[] PesosDv1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosDv2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>14 caracteres, sem máscara, em maiúsculas.</summary>
    public string Valor { get; }

    public string Formatado => $"{Valor[..2]}.{Valor[2..5]}.{Valor[5..8]}/{Valor[8..12]}-{Valor[12..]}";

    private Cnpj(string valor) => Valor = valor;

    public static Cnpj Parse(string? entrada) =>
        TryParse(entrada, out var cnpj) ? cnpj : throw new FormatException("CNPJ inválido.");

    public static bool TryParse(string? entrada, [NotNullWhen(true)] out Cnpj? cnpj)
    {
        cnpj = null;
        if (string.IsNullOrWhiteSpace(entrada)) return false;

        var normalizado = Normalizar(entrada);
        if (normalizado is null || !DigitosVerificadoresConferem(normalizado)) return false;

        cnpj = new Cnpj(normalizado);
        return true;
    }

    public override string ToString() => Formatado;

    private static string? Normalizar(string entrada)
    {
        Span<char> buffer = stackalloc char[Tamanho];
        var n = 0;
        foreach (var c in entrada)
        {
            if (c is '.' or '/' or '-' || char.IsWhiteSpace(c)) continue;
            if (n == Tamanho) return null;

            var maiusculo = char.ToUpperInvariant(c);
            var posicaoDv = n >= 12;
            var valido = char.IsAsciiDigit(maiusculo) || (!posicaoDv && char.IsAsciiLetterUpper(maiusculo));
            if (!valido) return null;

            buffer[n++] = maiusculo;
        }

        if (n != Tamanho) return null;
        // Sequências repetidas (00000000000000, AAAAAAAAAAAA00...) não são CNPJs reais.
        if (buffer[..12].IndexOfAnyExcept(buffer[0]) < 0) return null;

        return new string(buffer);
    }

    private static bool DigitosVerificadoresConferem(string cnpj) =>
        CalcularDv(cnpj.AsSpan(0, 12), PesosDv1) == cnpj[12] - '0'
        && CalcularDv(cnpj.AsSpan(0, 13), PesosDv2) == cnpj[13] - '0';

    private static int CalcularDv(ReadOnlySpan<char> base_, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < base_.Length; i++)
            soma += (base_[i] - '0') * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
