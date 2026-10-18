using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kamus.Modules.Financeiro.Aplicacao;

public sealed record ParcelaDoCarne(string Descricao, int Numero, int Parcelas, DateOnly Vencimento, decimal Valor);

/// <param name="Valor">Principal pago.</param>
public sealed record DadosDoRecibo(string Empresa, string? Cnpj, string Cliente, string Descricao, int Numero, int Parcelas, DateOnly Vencimento, DateOnly Data,
    decimal Valor, decimal Multa, decimal Juros, decimal Desconto, decimal SaldoRestante, string? Usuario);

/// <summary>Carnê (um canhoto por parcela, para o cliente levar) e recibo de pagamento do crediário, em PDF A4.</summary>
internal static class DocumentosDoCrediario
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    static DocumentosDoCrediario() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Moeda(decimal v) => v.ToString("C", PtBr);

    public static byte[] Carne(string empresa, string? cnpj, string cliente, IReadOnlyList<ParcelaDoCarne> parcelas) => Document.Create(doc => doc.Page(p =>
    {
        p.Size(PageSizes.A4);
        p.Margin(12, Unit.Millimetre);
        p.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));
        p.Header().PaddingBottom(6).Column(c =>
        {
            c.Item().Text($"Carnê do crediário — {cliente}").FontSize(13).Bold();
            c.Item().Text($"{empresa}{(cnpj is null ? "" : $" · CNPJ {cnpj}")}").FontColor(Colors.Grey.Darken2);
        });
        p.Content().Column(c =>
        {
            c.Spacing(6);
            if (parcelas.Count == 0) c.Item().Text("Nenhuma parcela em aberto.");
            foreach (var x in parcelas)
                c.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).Padding(8).Row(r =>
                {
                    r.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text(x.Descricao).Bold();
                        col.Item().Text($"Parcela {x.Numero} de {x.Parcelas} · {cliente}");
                    });
                    r.RelativeItem(2).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text($"Vencimento {x.Vencimento:dd/MM/yyyy}");
                        col.Item().AlignRight().Text(Moeda(x.Valor)).FontSize(13).Bold();
                    });
                });
        });
        p.Footer().AlignCenter().Text("Após o vencimento: multa e juros conforme as regras do crediário da loja.").FontSize(7).FontColor(Colors.Grey.Darken1);
    })).GeneratePdf();

    public static byte[] Recibo(DadosDoRecibo r) => Document.Create(doc => doc.Page(p =>
    {
        p.Size(PageSizes.A4);
        p.Margin(18, Unit.Millimetre);
        p.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));
        p.Content().Column(c =>
        {
            c.Spacing(6);
            c.Item().Text("Recibo de pagamento — crediário").FontSize(15).Bold();
            c.Item().Text($"{r.Empresa}{(r.Cnpj is null ? "" : $" · CNPJ {r.Cnpj}")}").FontColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(8).Text($"Recebemos de {r.Cliente} o valor de {Moeda(r.Valor + r.Multa + r.Juros - r.Desconto)}, referente a:");
            c.Item().Text($"{r.Descricao} · parcela {r.Numero} de {r.Parcelas} · vencimento {r.Vencimento:dd/MM/yyyy}").Bold();
            c.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn();
                    cd.ConstantColumn(120);
                });
                void Linha(string rotulo, decimal valor)
                {
                    t.Cell().PaddingVertical(2).Text(rotulo);
                    t.Cell().PaddingVertical(2).AlignRight().Text(Moeda(valor));
                }
                Linha("Parcela (principal)", r.Valor);
                if (r.Multa > 0) Linha("Multa por atraso", r.Multa);
                if (r.Juros > 0) Linha("Juros", r.Juros);
                if (r.Desconto > 0) Linha("Desconto", -r.Desconto);
                Linha("Total pago", r.Valor + r.Multa + r.Juros - r.Desconto);
                Linha("Saldo restante da parcela", r.SaldoRestante);
            });
            c.Item().PaddingTop(10).Text($"Data do pagamento: {r.Data:dd/MM/yyyy}{(r.Usuario is null ? "" : $" · recebido por {r.Usuario}")}");
            c.Item().PaddingTop(30).AlignCenter().Text("______________________________________");
            c.Item().AlignCenter().Text(r.Empresa).FontSize(9);
        });
    })).GeneratePdf();
}
