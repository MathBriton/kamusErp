using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kamus.Infrastructure.Etiquetas;

/// <param name="Detalhe">Linha de destaque (ex.: "CAM-001 · Azul · M").</param>
public sealed record Etiqueta(string Descricao, string Detalhe, decimal Preco, string CodigoDeBarras);

public enum FormatoDeEtiqueta
{
    /// <summary>Folha A4 com 3 colunas × 10 linhas (etiquetas adesivas de ~63 × 26 mm).</summary>
    A4,

    /// <summary>Impressora térmica: uma etiqueta de 40 × 25 mm por página.</summary>
    Termica,
}

/// <summary>PDF de etiquetas com descrição, referência/cor/tamanho, preço e código de barras (sempre preto no branco).</summary>
public static class GeradorDeEtiquetas
{
    public const int Maximo = 3000;
    private const int Colunas = 3;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    static GeradorDeEtiquetas() => QuestPDF.Settings.License = LicenseType.Community;

    public static bool TentarFormato(string? texto, out FormatoDeEtiqueta formato)
    {
        formato = FormatoDeEtiqueta.A4;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        return Enum.TryParse(texto, ignoreCase: true, out formato) && Enum.IsDefined(formato);
    }

    public static byte[] Gerar(IReadOnlyList<Etiqueta> etiquetas, FormatoDeEtiqueta formato)
    {
        ArgumentOutOfRangeException.ThrowIfZero(etiquetas.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(etiquetas.Count, Maximo);
        return formato == FormatoDeEtiqueta.Termica ? Termica(etiquetas) : A4(etiquetas);
    }

    private static byte[] Termica(IReadOnlyList<Etiqueta> etiquetas) => Document.Create(doc =>
    {
        foreach (var e in etiquetas)
            doc.Page(p =>
            {
                p.Size(40, 25, Unit.Millimetre);
                p.Margin(1.5f, Unit.Millimetre);
                p.DefaultTextStyle(t => t.FontSize(6).FontColor(Colors.Black));
                p.Content().Element(c => Conteudo(c, e, compacta: true));
            });
    }).GeneratePdf();

    private static byte[] A4(IReadOnlyList<Etiqueta> etiquetas) => Document.Create(doc => doc.Page(p =>
    {
        p.Size(PageSizes.A4);
        p.MarginVertical(12, Unit.Millimetre);
        p.MarginHorizontal(7, Unit.Millimetre);
        p.DefaultTextStyle(t => t.FontSize(7).FontColor(Colors.Black));
        p.Content().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (var i = 0; i < Colunas; i++) c.RelativeColumn();
            });
            // 10 linhas de 27,3 mm cabem nos 273 mm úteis da folha: a tabela quebra a página sozinha.
            foreach (var e in etiquetas)
                t.Cell().Height(27.3f, Unit.Millimetre).Padding(2, Unit.Millimetre).Element(c => Conteudo(c, e, compacta: false));
        });
    })).GeneratePdf();

    private static void Conteudo(IContainer container, Etiqueta e, bool compacta) => container.Column(col =>
    {
        col.Spacing(compacta ? 0.5f : 1);
        col.Item().Text(e.Descricao).ClampLines(1).FontSize(compacta ? 5.5f : 7);
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(e.Detalhe).Bold().ClampLines(1);
            r.AutoItem().Text(e.Preco.ToString("C", PtBr)).Bold().FontSize(compacta ? 8 : 10);
        });
        col.Item().Height(compacta ? 9 : 10, Unit.Millimetre).Svg(tamanho => DesenhoDeCodigoDeBarras.Svg(e.CodigoDeBarras, tamanho.Width, tamanho.Height));
        col.Item().AlignCenter().Text(e.CodigoDeBarras).FontSize(compacta ? 5 : 6).LetterSpacing(0.1f);
    });
}
