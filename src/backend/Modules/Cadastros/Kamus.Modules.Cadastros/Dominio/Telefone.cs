namespace Kamus.Modules.Cadastros.Dominio;

/// <summary>Telefone brasileiro guardado só com dígitos: DDD + número (10 ou 11 dígitos), sem o 55.</summary>
public static class Telefone
{
    /// <returns>Os dígitos normalizados, ou <c>null</c> se não for um telefone com DDD.</returns>
    public static string? Normalizar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;
        var digitos = new string(entrada.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal)) digitos = digitos[2..];
        return digitos.Length is 10 or 11 && digitos[0] != '0' ? digitos : null;
    }

    /// <summary>Número para links do WhatsApp (wa.me/55...).</summary>
    public static string? ParaWhatsApp(string? telefone) => telefone is null ? null : "55" + telefone;
}
