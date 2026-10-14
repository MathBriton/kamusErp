using ClosedXML.Excel;

namespace Kamus.Infrastructure.Planilhas;

/// <param name="Instrucao">Texto da aba "Instruções" (o que aceita, formato, se é obrigatório).</param>
/// <param name="Exemplo">Valor da linha de exemplo.</param>
public sealed record ColunaDoModelo(string Nome, string Instrucao, string Exemplo, bool Obrigatoria = false);

/// <summary>Modelo .xlsx para importação: aba "Dados" (cabeçalho + 1 linha de exemplo) e aba "Instruções".</summary>
public static class ModeloDePlanilha
{
    public static byte[] Gerar(string titulo, IReadOnlyList<ColunaDoModelo> colunas)
    {
        using var livro = new XLWorkbook();
        var dados = livro.Worksheets.Add("Dados");
        for (var i = 0; i < colunas.Count; i++)
        {
            var cabecalho = dados.Cell(1, i + 1);
            cabecalho.Value = colunas[i].Nome;
            cabecalho.Style.Font.SetBold();
            if (colunas[i].Obrigatoria) cabecalho.Style.Fill.SetBackgroundColor(XLColor.LightYellow);
            dados.Cell(2, i + 1).SetValue(colunas[i].Exemplo);
            dados.Cell(2, i + 1).Style.Font.SetFontColor(XLColor.Gray);
        }
        dados.Columns().AdjustToContents(10.0, 40.0);
        dados.SheetView.FreezeRows(1);

        var instrucoes = livro.Worksheets.Add("Instruções");
        instrucoes.Cell(1, 1).Value = $"Importação de {titulo.ToLowerInvariant()}";
        instrucoes.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        instrucoes.Cell(2, 1).Value = "Preencha a aba \"Dados\" a partir da linha 2 (apague a linha de exemplo). Colunas amarelas são obrigatórias. "
            + "Reimportar a mesma planilha atualiza os cadastros em vez de duplicar. Células vazias não apagam o que já existe.";
        instrucoes.Cell(4, 1).Value = "Coluna";
        instrucoes.Cell(4, 2).Value = "Como preencher";
        instrucoes.Range(4, 1, 4, 2).Style.Font.SetBold();
        for (var i = 0; i < colunas.Count; i++)
        {
            instrucoes.Cell(5 + i, 1).Value = colunas[i].Nome + (colunas[i].Obrigatoria ? " *" : "");
            instrucoes.Cell(5 + i, 2).Value = colunas[i].Instrucao;
        }
        instrucoes.Column(1).Width = 24;
        instrucoes.Column(2).Width = 90;

        using var saida = new MemoryStream();
        livro.SaveAs(saida);
        return saida.ToArray();
    }
}
