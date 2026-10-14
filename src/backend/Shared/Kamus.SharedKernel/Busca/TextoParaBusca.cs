using System.Globalization;
using System.Text;

namespace Kamus.SharedKernel.Busca;

/// <summary>Comparações de busca sem acento, sem diferenciar maiúsculas e ignorando máscaras de documento.</summary>
public static class TextoParaBusca
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var espacoAnterior = false;
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!espacoAnterior) sb.Append(' ');
                espacoAnterior = true;
                continue;
            }
            sb.Append(char.ToLowerInvariant(c));
            espacoAnterior = false;
        }
        return sb.ToString();
    }

    public static bool Contem(string? texto, string termo) =>
        Normalizar(texto).Contains(Normalizar(termo), StringComparison.Ordinal);

    /// <summary>Compara só os dígitos/letras (ex.: CNPJ "12.345.678/0001-95" contém "12345678").</summary>
    public static bool ContemDigitos(string? documento, string termo)
    {
        var termoLimpo = SomenteAlfanumericos(termo);
        return termoLimpo.Length > 0 && SomenteAlfanumericos(documento).Contains(termoLimpo, StringComparison.Ordinal);
    }

    private static string SomenteAlfanumericos(string? texto) =>
        new((texto ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
