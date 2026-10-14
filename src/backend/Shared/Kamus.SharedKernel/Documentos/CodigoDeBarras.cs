namespace Kamus.SharedKernel.Documentos;

/// <summary>
/// Códigos de barras GTIN (EAN-8, UPC-A, EAN-13, GTIN-14), conferidos pelo dígito verificador.
/// Códigos <b>internos</b> da loja: EAN-13 com prefixo 2 (faixa reservada para uso interno, não circula fora da loja).
/// </summary>
public static class CodigoDeBarras
{
    private const long MaximoInterno = 99_999_999_999;

    /// <summary>Só os dígitos (tira espaços); <c>null</c> se houver outros caracteres.</summary>
    public static string? Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var semEspacos = string.Concat(codigo.Where(c => !char.IsWhiteSpace(c)));
        return semEspacos.All(char.IsAsciiDigit) ? semEspacos : null;
    }

    public static bool Valido(string? codigo) =>
        Normalizar(codigo) is { Length: 8 or 12 or 13 or 14 } d && Dv(d.AsSpan(0, d.Length - 1)) == d[^1] - '0';

    public static string Interno(long sequencial)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequencial, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequencial, MaximoInterno);
        var base_ = "2" + sequencial.ToString("D11", System.Globalization.CultureInfo.InvariantCulture);
        return base_ + Dv(base_);
    }

    /// <returns>O sequencial de um código interno (prefixo 2), ou <c>null</c> se não for interno.</returns>
    public static long? SequencialInterno(string? codigo) =>
        codigo is { Length: 13 } && codigo[0] == '2' && Valido(codigo) ? long.Parse(codigo.AsSpan(1, 11), System.Globalization.CultureInfo.InvariantCulture) : null;

    /// <summary>Módulo 10 do GTIN: pesos 3 e 1 alternados, começando pela direita.</summary>
    private static int Dv(ReadOnlySpan<char> semDv)
    {
        var soma = 0;
        for (var i = 0; i < semDv.Length; i++)
            soma += (semDv[semDv.Length - 1 - i] - '0') * (i % 2 == 0 ? 3 : 1);
        return (10 - soma % 10) % 10;
    }
}
