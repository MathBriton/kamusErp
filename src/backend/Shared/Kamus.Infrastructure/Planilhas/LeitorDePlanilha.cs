using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Kamus.SharedKernel.Busca;

namespace Kamus.Infrastructure.Planilhas;

/// <summary>Planilha que não dá para ler (formato, arquivo corrompido, vazia). A mensagem é pronta para exibir.</summary>
public sealed class PlanilhaInvalidaException : Exception
{
    public PlanilhaInvalidaException() { }

    public PlanilhaInvalidaException(string mensagem) : base(mensagem) { }

    public PlanilhaInvalidaException(string mensagem, Exception interna) : base(mensagem, interna) { }
}

/// <param name="Numero">Número da linha como o usuário vê no Excel (o cabeçalho é a linha 1).</param>
public sealed record LinhaLida(int Numero, IReadOnlyDictionary<string, string> Valores)
{
    /// <param name="coluna">Nome normalizado (sem acento, minúsculo, só letras e números). Vazio vira <c>null</c>.</param>
    public string? Valor(string coluna) => Valores.TryGetValue(coluna, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public string? Valor(params string[] alternativas) => alternativas.Select(Valor).FirstOrDefault(v => v is not null);
}

public sealed record TabelaLida(IReadOnlyList<string> Colunas, IReadOnlyList<LinhaLida> Linhas);

/// <summary>
/// Lê a primeira aba de um .xlsx ou um .csv (separador ; ou ,). A primeira linha é o cabeçalho; o nome das colunas é
/// normalizado ("CPF/CNPJ" → "cpfcnpj", "Preço de venda" → "precodevenda"), então a ordem das colunas não importa.
/// Números saem em formato invariante ("59.9") e datas como "aaaa-mm-dd". Linhas em branco são ignoradas.
/// </summary>
public static class LeitorDePlanilha
{
    public const int MaximoDeLinhas = 5000;

    public static string NormalizarColuna(string? nome) =>
        new(TextoParaBusca.Normalizar(nome).Where(char.IsLetterOrDigit).ToArray());

    public static TabelaLida Ler(Stream arquivo, string nomeDoArquivo)
    {
        var extensao = Path.GetExtension(nomeDoArquivo).ToLowerInvariant();
        var linhas = extensao switch
        {
            ".xlsx" => LerXlsx(arquivo),
            ".csv" => LerCsv(arquivo),
            _ => throw new PlanilhaInvalidaException("Envie um arquivo .xlsx (Excel) ou .csv."),
        };
        if (linhas.Count == 0) throw new PlanilhaInvalidaException("A planilha está vazia.");

        var colunas = linhas[0].Celulas.Select(NormalizarColuna).ToList();
        var dados = linhas.Skip(1)
            .Where(l => l.Celulas.Any(c => !string.IsNullOrWhiteSpace(c)))
            .Select(l =>
            {
                var valores = new Dictionary<string, string>();
                for (var i = 0; i < colunas.Count && i < l.Celulas.Count; i++)
                    if (colunas[i].Length > 0) valores.TryAdd(colunas[i], l.Celulas[i]);
                return new LinhaLida(l.Numero, valores);
            })
            .ToList();
        if (dados.Count > MaximoDeLinhas)
            throw new PlanilhaInvalidaException($"A planilha tem {dados.Count} linhas; o máximo por importação é {MaximoDeLinhas}. Divida em arquivos menores.");
        return new TabelaLida(colunas, dados);
    }

    private sealed record LinhaBruta(int Numero, IReadOnlyList<string> Celulas);

    private static List<LinhaBruta> LerXlsx(Stream arquivo)
    {
        XLWorkbook livro;
        try
        {
            livro = new XLWorkbook(arquivo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new PlanilhaInvalidaException("Não foi possível abrir o arquivo como Excel (.xlsx). Ele pode estar corrompido.", ex);
        }

        using (livro)
        {
            var aba = livro.Worksheet(1);
            var usadas = aba.RangeUsed();
            if (usadas is null) return [];
            var ultimaColuna = usadas.LastColumn().ColumnNumber();
            return aba.RowsUsed()
                .Select(r => new LinhaBruta(r.RowNumber(), Enumerable.Range(1, ultimaColuna).Select(c => Texto(r.Cell(c))).ToList()))
                .ToList();
        }
    }

    private static string Texto(IXLCell celula)
    {
        var v = celula.Value;
        if (v.IsBlank) return "";
        if (v.IsNumber) return v.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (v.IsBoolean) return v.GetBoolean() ? "Sim" : "Não";
        return celula.GetFormattedString();
    }

    private static List<LinhaBruta> LerCsv(Stream arquivo)
    {
        using var leitor = new StreamReader(arquivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var conteudo = leitor.ReadToEnd();
        var primeira = conteudo.Split('\n', 2)[0];
        var separador = primeira.Count(c => c == ';') >= primeira.Count(c => c == ',') ? ';' : ',';

        var linhas = new List<LinhaBruta>();
        var celulas = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;
        var numero = 1;
        var inicioDaLinha = 1;

        for (var i = 0; i < conteudo.Length; i++)
        {
            var c = conteudo[i];
            if (entreAspas)
            {
                if (c == '"' && i + 1 < conteudo.Length && conteudo[i + 1] == '"') { atual.Append('"'); i++; }
                else if (c == '"') entreAspas = false;
                else { if (c == '\n') numero++; atual.Append(c); }
                continue;
            }
            switch (c)
            {
                case '"': entreAspas = true; break;
                case '\r': break;
                case '\n':
                    celulas.Add(atual.ToString());
                    linhas.Add(new LinhaBruta(inicioDaLinha, celulas));
                    celulas = [];
                    atual.Clear();
                    inicioDaLinha = ++numero;
                    break;
                default:
                    if (c == separador) { celulas.Add(atual.ToString()); atual.Clear(); }
                    else atual.Append(c);
                    break;
            }
        }
        if (atual.Length > 0 || celulas.Count > 0)
        {
            celulas.Add(atual.ToString());
            linhas.Add(new LinhaBruta(inicioDaLinha, celulas));
        }
        return linhas;
    }
}
