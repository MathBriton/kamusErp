using System.Globalization;
using System.Text;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Infrastructure.Relatorios;

/// <summary>Quem gerou, para qual empresa e quando — vai no topo do PDF e do Excel.</summary>
/// <param name="Cnpj">Nulo na visão consolidada.</param>
public sealed record CabecalhoDoRelatorio(string Empresa, string? Cnpj, string Usuario, DateTimeOffset GeradoEm);

public interface IExportadorDeRelatorio
{
    string Formato { get; }
    string ContentType { get; }
    byte[] Exportar(DadosDoRelatorio dados, CabecalhoDoRelatorio cabecalho);
}

/// <summary>Formatação brasileira compartilhada pelos exportadores.</summary>
internal static class FormatoBr
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTime ParaBrasilia(DateTimeOffset valor) => TimeZoneInfo.ConvertTime(valor, Brasilia).DateTime;

    public static string Texto(object? valor, TipoColuna tipo) => valor switch
    {
        null => "",
        DateTimeOffset d => ParaBrasilia(d).ToString("dd/MM/yyyy HH:mm", Cultura),
        DateTime d => d.ToString(tipo == TipoColuna.Data ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm", Cultura),
        DateOnly d => d.ToString("dd/MM/yyyy", Cultura),
        _ when tipo == TipoColuna.Moeda => Convert.ToDecimal(valor, Cultura).ToString("C2", Cultura),
        _ when tipo == TipoColuna.Numero => Convert.ToDecimal(valor, Cultura).ToString("N2", Cultura),
        _ when tipo == TipoColuna.Inteiro => Convert.ToDecimal(valor, Cultura).ToString("N0", Cultura),
        _ => Convert.ToString(valor, Cultura) ?? "",
    };

    public static bool Numerica(TipoColuna tipo) => tipo is TipoColuna.Inteiro or TipoColuna.Numero or TipoColuna.Moeda;

    public static decimal? Total(DadosDoRelatorio dados, int coluna) =>
        dados.Colunas[coluna].Totalizar
            ? dados.Linhas.Sum(l => l[coluna] is null ? 0m : Convert.ToDecimal(l[coluna], Cultura))
            : null;

    public static string Empresa(CabecalhoDoRelatorio c) => c.Cnpj is null ? c.Empresa : $"{c.Empresa} · CNPJ {c.Cnpj}";

    public static string Geracao(CabecalhoDoRelatorio c) =>
        $"Gerado em {ParaBrasilia(c.GeradoEm).ToString("dd/MM/yyyy HH:mm", Cultura)} por {c.Usuario}";
}

/// <summary>
/// CSV para importar em outros sistemas: só cabeçalho e dados, separador ";", vírgula decimal sem milhar,
/// UTF-8 com BOM (o Excel brasileiro abre com acentos e colunas corretas).
/// </summary>
public sealed class ExportadorCsv : IExportadorDeRelatorio
{
    public string Formato => "csv";
    public string ContentType => "text/csv; charset=utf-8";

    public byte[] Exportar(DadosDoRelatorio dados, CabecalhoDoRelatorio cabecalho)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', dados.Colunas.Select(c => Escapar(c.Titulo)))).Append("\r\n");
        foreach (var linha in dados.Linhas)
        {
            var celulas = dados.Colunas.Select((c, i) => Escapar(Valor(linha[i], c.Tipo)));
            sb.Append(string.Join(';', celulas)).Append("\r\n");
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Valor(object? valor, TipoColuna tipo) =>
        valor is not null && FormatoBr.Numerica(tipo)
            ? Convert.ToDecimal(valor, FormatoBr.Cultura).ToString(tipo == TipoColuna.Inteiro ? "0" : "0.00", FormatoBr.Cultura)
            : FormatoBr.Texto(valor, tipo);

    private static string Escapar(string valor) =>
        valor.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : valor;
}
