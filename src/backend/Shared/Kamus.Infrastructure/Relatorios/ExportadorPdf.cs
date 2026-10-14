using Kamus.SharedKernel.Relatorios;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kamus.Infrastructure.Relatorios;

/// <summary>
/// PDF para impressão: sempre no tema claro, cabeçalho com empresa e filtros, total e paginação.
/// QuestPDF: licença Community (gratuita para empresas com faturamento anual abaixo de US$ 1 milhão).
/// </summary>
public sealed class ExportadorPdf : IExportadorDeRelatorio
{
    private const string CorPrimaria = "#4F46E5";
    private const string CorSuave = "#6B6B78";
    private const string CorBorda = "#E5E5EA";

    static ExportadorPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public string Formato => "pdf";
    public string ContentType => "application/pdf";

    public byte[] Exportar(DadosDoRelatorio dados, CabecalhoDoRelatorio cabecalho)
    {
        var paisagem = dados.Colunas.Count > 5;
        return Document.Create(doc => doc.Page(pagina =>
        {
            pagina.Size(paisagem ? PageSizes.A4.Landscape() : PageSizes.A4);
            pagina.Margin(28);
            pagina.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));

            pagina.Header().PaddingBottom(10).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(dados.Titulo).FontSize(16).Bold();
                    row.AutoItem().AlignRight().Text("kamus").FontSize(12).Bold().FontColor(CorPrimaria);
                });
                col.Item().Text(FormatoBr.Empresa(cabecalho)).FontColor(CorSuave);
                if (dados.Filtros.Count > 0)
                    col.Item().Text(string.Join("   ", dados.Filtros.Select(f => $"{f.Nome}: {f.Valor}"))).FontColor(CorSuave);
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(CorBorda);
            });

            pagina.Content().Table(tabela =>
            {
                tabela.ColumnsDefinition(c =>
                {
                    foreach (var coluna in dados.Colunas)
                        c.RelativeColumn(coluna.Largura ?? (coluna.Tipo == TipoColuna.Texto ? 2 : 1));
                });

                tabela.Header(h =>
                {
                    foreach (var coluna in dados.Colunas)
                        Alinhar(h.Cell().Background("#EEEFFE").PaddingVertical(4).PaddingHorizontal(4), coluna.Tipo)
                            .Text(coluna.Titulo).Bold();
                });

                foreach (var linha in dados.Linhas)
                    for (var i = 0; i < dados.Colunas.Count; i++)
                        Alinhar(tabela.Cell().BorderBottom(0.5f).BorderColor(CorBorda).PaddingVertical(3).PaddingHorizontal(4), dados.Colunas[i].Tipo)
                            .Text(FormatoBr.Texto(linha[i], dados.Colunas[i].Tipo));

                if (dados.Colunas.Any(c => c.Totalizar))
                    for (var i = 0; i < dados.Colunas.Count; i++)
                    {
                        var texto = i == 0 ? "Total" : FormatoBr.Total(dados, i) is { } t ? FormatoBr.Texto(t, dados.Colunas[i].Tipo) : "";
                        Alinhar(tabela.Cell().PaddingVertical(4).PaddingHorizontal(4), dados.Colunas[i].Tipo).Text(texto).Bold();
                    }

                if (dados.Linhas.Count == 0)
                    tabela.Cell().ColumnSpan((uint)dados.Colunas.Count).PaddingVertical(12).AlignCenter()
                        .Text("Nenhum registro encontrado com os filtros aplicados.").FontColor(CorSuave);
            });

            pagina.Footer().Row(row =>
            {
                row.RelativeItem().Text(FormatoBr.Geracao(cabecalho)).FontSize(8).FontColor(CorSuave);
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(CorSuave));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static IContainer Alinhar(IContainer celula, TipoColuna tipo) =>
        FormatoBr.Numerica(tipo) ? celula.AlignRight() : celula.AlignLeft();
}
