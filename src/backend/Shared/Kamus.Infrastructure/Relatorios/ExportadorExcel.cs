using ClosedXML.Excel;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Infrastructure.Relatorios;

/// <summary>Excel de verdade: números como número, datas como data, R$ formatado, filtro e cabeçalho congelado.</summary>
public sealed class ExportadorExcel : IExportadorDeRelatorio
{
    public string Formato => "xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public byte[] Exportar(DadosDoRelatorio dados, CabecalhoDoRelatorio cabecalho)
    {
        using var planilha = new XLWorkbook();
        var aba = planilha.Worksheets.Add("Relatório");
        var ultimaColuna = Math.Max(1, dados.Colunas.Count);

        aba.Cell(1, 1).Value = dados.Titulo;
        aba.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        aba.Cell(2, 1).Value = FormatoBr.Empresa(cabecalho);
        aba.Cell(3, 1).Value = FormatoBr.Geracao(cabecalho);
        var linha = 4;
        if (dados.Filtros.Count > 0)
            aba.Cell(linha++, 1).Value = string.Join("   ", dados.Filtros.Select(f => $"{f.Nome}: {f.Valor}"));
        for (var l = 2; l < linha; l++) aba.Cell(l, 1).Style.Font.SetFontColor(XLColor.Gray);

        var linhaCabecalho = linha + 1;
        for (var c = 0; c < dados.Colunas.Count; c++)
        {
            var celula = aba.Cell(linhaCabecalho, c + 1);
            celula.Value = dados.Colunas[c].Titulo;
            celula.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EEEFFE"));
        }

        var atual = linhaCabecalho;
        foreach (var registro in dados.Linhas)
        {
            atual++;
            for (var c = 0; c < dados.Colunas.Count; c++)
                Preencher(aba.Cell(atual, c + 1), registro[c], dados.Colunas[c].Tipo);
        }

        if (dados.Colunas.Any(c => c.Totalizar))
        {
            atual++;
            aba.Cell(atual, 1).Value = "Total";
            for (var c = 0; c < dados.Colunas.Count; c++)
                if (FormatoBr.Total(dados, c) is { } total)
                    Preencher(aba.Cell(atual, c + 1), total, dados.Colunas[c].Tipo);
            aba.Row(atual).Style.Font.SetBold();
        }

        var ultimaLinhaDeDados = linhaCabecalho + Math.Max(1, dados.Linhas.Count);
        aba.Range(linhaCabecalho, 1, ultimaLinhaDeDados, ultimaColuna).SetAutoFilter();
        aba.SheetView.FreezeRows(linhaCabecalho);
        aba.Columns(1, ultimaColuna).AdjustToContents(linhaCabecalho, atual);

        using var saida = new MemoryStream();
        planilha.SaveAs(saida);
        return saida.ToArray();
    }

    private static void Preencher(IXLCell celula, object? valor, TipoColuna tipo)
    {
        switch (valor)
        {
            case null:
                return;
            case DateTimeOffset d:
                celula.Value = FormatoBr.ParaBrasilia(d);
                celula.Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                return;
            case DateTime d:
                celula.Value = d;
                celula.Style.NumberFormat.Format = tipo == TipoColuna.Data ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
                return;
            case DateOnly d:
                celula.Value = d.ToDateTime(TimeOnly.MinValue);
                celula.Style.NumberFormat.Format = "dd/mm/yyyy";
                return;
        }

        if (FormatoBr.Numerica(tipo))
        {
            celula.Value = Convert.ToDecimal(valor, FormatoBr.Cultura);
            celula.Style.NumberFormat.Format = tipo switch
            {
                TipoColuna.Moeda => "\"R$\" #,##0.00",
                TipoColuna.Numero => "#,##0.00",
                _ => "#,##0",
            };
            return;
        }

        celula.Value = Convert.ToString(valor, FormatoBr.Cultura);
    }
}
