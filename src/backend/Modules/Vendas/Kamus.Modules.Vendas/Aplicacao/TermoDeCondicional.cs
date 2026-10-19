using System.Globalization;
using Kamus.Modules.Vendas.Dominio;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kamus.Modules.Vendas.Aplicacao;

/// <summary>Termo de responsabilidade da condicional (peças, valores e prazo) para o cliente assinar, em PDF A4.</summary>
internal static class TermoDeCondicional
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Colunas = ["Peça", "Qtd.", "Preço", "Total"];

    static TermoDeCondicional() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Moeda(decimal v) => v.ToString("C", PtBr);

    public static byte[] Gerar(Condicional c, string empresa, string? cnpj) => Document.Create(doc => doc.Page(p =>
    {
        p.Size(PageSizes.A4);
        p.Margin(16, Unit.Millimetre);
        p.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));
        p.Content().Column(col =>
        {
            col.Spacing(6);
            col.Item().Text($"Termo de condicional nº {c.Numero}").FontSize(15).Bold();
            col.Item().Text($"{empresa}{(cnpj is null ? "" : $" · CNPJ {cnpj}")}").FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(6).Text($"Cliente: {c.ClienteNome}").Bold();
            col.Item().Text($"Saída: {c.Data:dd/MM/yyyy} · Devolução até: {c.Prazo:dd/MM/yyyy}{(c.VendedorNome is null ? "" : $" · Vendedor: {c.VendedorNome}")}");
            col.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(4);
                    cd.RelativeColumn(1);
                    cd.RelativeColumn(1.5f);
                    cd.RelativeColumn(1.5f);
                });
                t.Header(h =>
                {
                    h.Cell().BorderBottom(1).PaddingVertical(3).Text(Colunas[0]).Bold();
                    foreach (var titulo in Colunas.Skip(1))
                        h.Cell().BorderBottom(1).PaddingVertical(3).AlignRight().Text(titulo).Bold();
                });
                foreach (var i in c.Itens)
                {
                    t.Cell().PaddingVertical(2).Text(i.Descricao);
                    t.Cell().PaddingVertical(2).AlignRight().Text(i.Quantidade.ToString(PtBr));
                    t.Cell().PaddingVertical(2).AlignRight().Text(Moeda(i.PrecoUnitario));
                    t.Cell().PaddingVertical(2).AlignRight().Text(Moeda(i.Quantidade * i.PrecoUnitario));
                }
                t.Cell().ColumnSpan(3).BorderTop(1).PaddingTop(3).Text($"Total ({c.Pecas} peças)").Bold();
                t.Cell().BorderTop(1).PaddingTop(3).AlignRight().Text(Moeda(c.Valor)).Bold();
            });
            col.Item().PaddingTop(10).Text(
                $"Declaro ter recebido as peças acima em condicional, comprometendo-me a devolvê-las em perfeito estado até {c.Prazo:dd/MM/yyyy} ou a pagar pelas que ficarem comigo, pelos preços indicados.");
            col.Item().PaddingTop(36).AlignCenter().Text("______________________________________");
            col.Item().AlignCenter().Text(c.ClienteNome).FontSize(9);
        });
    })).GeneratePdf();
}
