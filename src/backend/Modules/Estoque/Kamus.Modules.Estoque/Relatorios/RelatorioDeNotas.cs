using System.Globalization;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Estoque.Relatorios;

/// <summary>Notas de entrada (XML da NF-e) com os filtros da tela — situacao, busca, de/ate — ou só as selecionadas (ids).</summary>
internal sealed class RelatorioDeNotas(EstoqueDbContext db, IEmpresaAtiva empresa) : IRelatorio
{
    public string Codigo => "notas-de-entrada";
    public string Titulo => "Notas de entrada";
    public IReadOnlyCollection<string> Perfis => Permissoes.Movimentar;
    public string Area => "Estoque";
    public string? Descricao => "Notas fiscais de entrada importadas pelo XML, com fornecedor, valor e situação.";

    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("periodo", "Emissão", TipoDeParametro.Periodo, "mes"),
        new("situacao", "Situação", TipoDeParametro.Opcao, "todas",
            [new("todas", "Todas"), new("pendentes", "Em conferência"), new("lancadas", "Lançadas"), new("descartadas", "Descartadas")]),
    ];

    private static DateOnly? Data(IReadOnlyDictionary<string, string?> p, string chave) =>
        DateOnly.TryParseExact(p.GetValueOrDefault(chave), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Situacao(NotaDeEntrada n) => n.Status switch
    {
        StatusDaNota.Lancada => "Lançada",
        StatusDaNota.Descartada => "Descartada",
        _ => n.Itens.Any(i => i.Situacao == SituacaoDoItem.Pendente) ? "Em conferência" : "Pronta para lançar",
    };

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var ids = ParametrosDoRelatorio.Ids(parametros);
        // "ids" = notas selecionadas na grade: exporta só elas, ignorando os demais filtros.
        var selecao = ids.Count > 0;
        var busca = selecao ? null : parametros.GetValueOrDefault("busca");
        var (de, ate) = selecao ? (null, null) : (Data(parametros, "de"), Data(parametros, "ate"));
        var situacao = selecao ? NotasEndpoints.InclusiveDescartadas : parametros.GetValueOrDefault("situacao");
        var notas = await NotasEndpoints.FiltrarAsync(db, empresa.EmpresaIds, situacao, busca, de, ate, ct);
        if (selecao) notas = [.. notas.Where(n => ids.Contains(n.Id))];

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(notas.Count, "nota", "notas"));
        else
        {
            if (de is not null || ate is not null)
                filtros.Add(new("Emissão", $"{de?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "início"} a {ate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "hoje"}"));
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        }
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nota", Largura: 0.8f),
            new ColunaRelatorio("Série", Largura: 0.5f),
            new ColunaRelatorio("Emissão", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Fornecedor", Largura: 2f),
            new ColunaRelatorio("CNPJ/CPF", Largura: 1.2f),
            new ColunaRelatorio("Valor", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
            new ColunaRelatorio("Itens", TipoColuna.Inteiro, Largura: 0.5f),
            new ColunaRelatorio("Situação", Largura: 1.1f),
            new ColunaRelatorio("Importada em", TipoColuna.DataHora, Largura: 1f),
        ], [.. notas.Select(n => (IReadOnlyList<object?>)
        [
            n.Numero, n.Serie, DateOnly.FromDateTime(n.EmitidaEm.ToOffset(TimeSpan.FromHours(-3)).DateTime), n.FornecedorNome, n.FornecedorDocumento,
            n.ValorTotal, n.Itens.Count, Situacao(n), n.ImportadaEm,
        ])], filtros);
    }
}
