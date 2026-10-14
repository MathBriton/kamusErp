using System.Text;
using ClosedXML.Excel;
using Kamus.Infrastructure.Planilhas;

namespace Kamus.UnitTests.Planilhas;

public class LeitorDePlanilhaTests
{
    private static MemoryStream Xlsx(Action<IXLWorksheet> preencher)
    {
        using var livro = new XLWorkbook();
        preencher(livro.Worksheets.Add("Dados"));
        var ms = new MemoryStream();
        livro.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Xlsx_CabecalhoSemAcentoEMascaraLinhasNumeradasComoNoExcel()
    {
        using var arquivo = Xlsx(a =>
        {
            a.Cell(1, 1).Value = "Nome / Razão social";
            a.Cell(1, 2).Value = "CPF/CNPJ";
            a.Cell(1, 3).Value = "Preço de venda";
            a.Cell(1, 4).Value = "Nascimento";
            a.Cell(2, 1).Value = "Maria";
            a.Cell(2, 2).Value = "529.982.247-25";
            a.Cell(2, 3).Value = 59.9;
            a.Cell(2, 4).Value = new DateTime(1990, 10, 14);
            a.Cell(4, 1).Value = "João"; // linha 3 em branco é ignorada
        });

        var tabela = LeitorDePlanilha.Ler(arquivo, "clientes.xlsx");

        tabela.Linhas.Count.ShouldBe(2);
        var maria = tabela.Linhas[0];
        maria.Numero.ShouldBe(2);
        maria.Valor("nomerazaosocial").ShouldBe("Maria");
        maria.Valor("cpfcnpj").ShouldBe("529.982.247-25");
        maria.Valor("precodevenda").ShouldBe("59.9");
        maria.Valor("nascimento").ShouldBe("1990-10-14");
        tabela.Linhas[1].Numero.ShouldBe(4);
        tabela.Linhas[1].Valor("naoexiste").ShouldBeNull();
    }

    [Fact]
    public void Csv_PontoEVirgulaComAspasEBom()
    {
        var csv = "﻿Nome;Observações\nMaria;\"Gosta de azul; e verde\"\n\nAna;\n";
        using var arquivo = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var tabela = LeitorDePlanilha.Ler(arquivo, "clientes.csv");

        tabela.Linhas.Count.ShouldBe(2);
        tabela.Linhas[0].Valor("observacoes").ShouldBe("Gosta de azul; e verde");
        tabela.Linhas[1].Numero.ShouldBe(4);
        tabela.Linhas[1].Valor("observacoes").ShouldBeNull();
    }

    [Fact]
    public void Csv_VirgulaComoSeparador()
    {
        using var arquivo = new MemoryStream(Encoding.UTF8.GetBytes("Nome,Cidade\nMaria,Santos\n"));

        LeitorDePlanilha.Ler(arquivo, "a.csv").Linhas[0].Valor("cidade").ShouldBe("Santos");
    }

    [Fact]
    public void FormatoNaoSuportado_Recusa()
    {
        using var arquivo = new MemoryStream([1, 2, 3]);

        Should.Throw<PlanilhaInvalidaException>(() => LeitorDePlanilha.Ler(arquivo, "a.pdf")).Message.ShouldContain(".xlsx");
    }

    [Fact]
    public void XlsxCorrompido_Recusa()
    {
        using var arquivo = new MemoryStream(Encoding.UTF8.GetBytes("não é excel"));

        Should.Throw<PlanilhaInvalidaException>(() => LeitorDePlanilha.Ler(arquivo, "a.xlsx"));
    }

    [Fact]
    public void ModeloDePlanilha_TemCabecalhoExemploEInstrucoes()
    {
        var bytes = ModeloDePlanilha.Gerar("Clientes",
        [
            new ColunaDoModelo("Nome", "Obrigatório", "Maria Silva", Obrigatoria: true),
            new ColunaDoModelo("CPF/CNPJ", "Opcional", "529.982.247-25"),
        ]);

        using var livro = new XLWorkbook(new MemoryStream(bytes));
        var dados = livro.Worksheet(1);
        dados.Cell(1, 1).GetString().ShouldBe("Nome");
        dados.Cell(2, 1).GetString().ShouldBe("Maria Silva");
        livro.Worksheets.Count.ShouldBe(2);
        livro.Worksheet(2).Name.ShouldBe("Instruções");
        // O próprio modelo, relido, traz o exemplo como primeira linha de dados.
        LeitorDePlanilha.Ler(new MemoryStream(bytes), "modelo.xlsx").Linhas.ShouldHaveSingleItem().Valor("nome").ShouldBe("Maria Silva");
    }
}
