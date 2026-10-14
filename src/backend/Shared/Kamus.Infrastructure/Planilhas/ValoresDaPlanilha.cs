using System.Globalization;

namespace Kamus.Infrastructure.Planilhas;

/// <summary>Conversões tolerantes do que vem da planilha (o leitor entrega números invariantes e datas aaaa-mm-dd).</summary>
public static class ValoresDaPlanilha
{
    private static readonly string[] FormatosDeData = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy"];

    /// <returns><c>(null, true)</c> se vazio; <c>(null, false)</c> se inválido.</returns>
    public static (decimal? Valor, bool Ok) Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, true);
        var t = texto.Replace("R$", "", StringComparison.Ordinal).Trim();
        if (t.Contains(',', StringComparison.Ordinal)) t = t.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? (Math.Round(v, 2), true) : (null, false);
    }

    public static (DateOnly? Valor, bool Ok) Data(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, true);
        return DateOnly.TryParseExact(texto.Trim(), FormatosDeData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (d, true) : (null, false);
    }

    /// <returns>Sim/Não; <c>null</c> se vazio ou não reconhecido.</returns>
    public static bool? SimNao(string? texto) => Kamus.SharedKernel.Busca.TextoParaBusca.Normalizar(texto) switch
    {
        "sim" or "s" or "x" or "true" or "1" or "yes" => true,
        "nao" or "n" or "false" or "0" or "no" => false,
        _ => null,
    };
}
