using System.Text;
using ClosedXML.Excel;
using Kamus.Infrastructure.Relatorios;
using Kamus.SharedKernel.Relatorios;
using UglyToad.PdfPig;

namespace Kamus.UnitTests.Relatorios;

public class ExportadoresTests
{
    private static readonly DadosDoRelatorio Dados = new(
        "Vendas por produto",
        [
            new ColunaRelatorio("Produto"),
            new ColunaRelatorio("Quantidade", TipoColuna.Inteiro, Totalizar: true),
            new ColunaRelatorio("Valor", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Data", TipoColuna.Data),
            new ColunaRelatorio("Atualizado em", TipoColuna.DataHora),
        ],
        [
            ["Camiseta Básica", 3, 59.90m, new DateOnly(2026, 10, 14), new DateTimeOffset(2026, 10, 14, 15, 30, 0, TimeSpan.Zero)],
            ["Calça; Jeans \"Skinny\"", 2, 1234.56m, new DateOnly(2026, 10, 13), null],
        ],
        [new FiltroAplicado("Período", "01/10/2026 a 31/10/2026")]);

    private static readonly CabecalhoDoRelatorio Cabecalho = new(
        "Kamus Moda Centro", "12.345.678/0001-95", "Admin Demo", new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Excel_TemCabecalhoDaEmpresaFiltrosETiposReais()
    {
        var bytes = new ExportadorExcel().Exportar(Dados, Cabecalho);

        using var planilha = new XLWorkbook(new MemoryStream(bytes));
        var aba = planilha.Worksheets.First();
        var textoDoTopo = string.Join(" | ", Enumerable.Range(1, 4).Select(l => aba.Cell(l, 1).GetString()));
        textoDoTopo.ShouldContain("Vendas por produto");
        textoDoTopo.ShouldContain("Kamus Moda Centro");
        textoDoTopo.ShouldContain("12.345.678/0001-95");
        textoDoTopo.ShouldContain("Período: 01/10/2026 a 31/10/2026");

        var cabecalho = aba.CellsUsed().First(c => c.GetString() == "Produto");
        var linha = cabecalho.Address.RowNumber + 1;
        aba.Cell(linha, 1).GetString().ShouldBe("Camiseta Básica");
        aba.Cell(linha, 2).DataType.ShouldBe(XLDataType.Number);
        aba.Cell(linha, 3).GetValue<decimal>().ShouldBe(59.90m);
        aba.Cell(linha, 3).Style.NumberFormat.Format.ShouldContain("R$");
        aba.Cell(linha, 4).DataType.ShouldBe(XLDataType.DateTime);
        aba.Cell(linha, 4).GetDateTime().ShouldBe(new DateTime(2026, 10, 14));
        aba.Cell(linha, 5).GetDateTime().ShouldBe(new DateTime(2026, 10, 14, 12, 30, 0)); // horário de Brasília
    }

    [Fact]
    public void Excel_TemLinhaDeTotalFiltroAutomaticoECabecalhoCongelado()
    {
        var bytes = new ExportadorExcel().Exportar(Dados, Cabecalho);

        using var planilha = new XLWorkbook(new MemoryStream(bytes));
        var aba = planilha.Worksheets.First();
        var total = aba.CellsUsed().First(c => c.GetString() == "Total");
        aba.Cell(total.Address.RowNumber, 2).GetValue<decimal>().ShouldBe(5);
        aba.Cell(total.Address.RowNumber, 3).GetValue<decimal>().ShouldBe(1294.46m);
        aba.AutoFilter.IsEnabled.ShouldBeTrue();
        aba.SheetView.SplitRow.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Csv_SoDadosComSeparadorPontoEVirgulaEPadraoBrasileiro()
    {
        var bytes = new ExportadorCsv().Exportar(Dados, Cabecalho);

        bytes.Take(3).ShouldBe(Encoding.UTF8.GetPreamble()); // BOM: o Excel abre com acentos corretos
        var linhas = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        linhas[0].ShouldBe("Produto;Quantidade;Valor;Data;Atualizado em");
        linhas[1].ShouldBe("Camiseta Básica;3;59,90;14/10/2026;14/10/2026 12:30");
        linhas[2].ShouldBe("\"Calça; Jeans \"\"Skinny\"\"\";2;1234,56;13/10/2026;");
        linhas.Length.ShouldBe(3);
    }

    [Fact]
    public void Pdf_TemTituloEmpresaFiltrosDadosFormatadosTotalERodape()
    {
        var bytes = new ExportadorPdf().Exportar(Dados, Cabecalho);

        Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
        using var pdf = PdfDocument.Open(bytes);
        var texto = string.Join(" ", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
        foreach (var esperado in new[] { "Vendas", "Kamus", "12.345.678/0001-95", "Camiseta", "59,90", "1.294,46", "Total", "Admin", "01/10/2026", "Página" })
            texto.ShouldContain(esperado);
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("csv", "text/csv")]
    public void Exportadores_DeclaramFormatoETipoDeConteudo(string formato, string contentType)
    {
        IExportadorDeRelatorio[] todos = [new ExportadorPdf(), new ExportadorExcel(), new ExportadorCsv()];

        todos.Single(e => e.Formato == formato).ContentType.ShouldStartWith(contentType);
    }
}
