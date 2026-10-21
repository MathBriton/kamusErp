using System.Globalization;
using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Estoque.Relatorios;

/// <summary>Lista de inventários (situação, loja, abertura e contagem) com os filtros da tela ou só os selecionados (ids).
/// As divergências de um inventário saem pelo relatório <c>inventario</c>.</summary>
internal sealed class RelatorioDeInventarios(Lojas lojas, EstoqueDbContext db) : IRelatorio
{
    public string Codigo => "inventarios";
    public string Titulo => "Inventários";
    public IReadOnlyCollection<string> Perfis => Permissoes.Movimentar;
    public string Area => "Estoque";
    public string? Descricao => "Inventários abertos, concluídos e cancelados, com o que foi contado.";

    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("periodo", "Abertura", TipoDeParametro.Periodo, "mes"),
        new("situacao", "Situação", TipoDeParametro.Opcao, "todos",
            [new("todos", "Todas"), new("Aberto", "Em contagem"), new("Concluido", "Concluídos"), new("Cancelado", "Cancelados")]),
    ];

    private static DateOnly? Data(IReadOnlyDictionary<string, string?> p, string chave) =>
        DateOnly.TryParseExact(p.GetValueOrDefault(chave), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Situacao(string status) => status switch
    {
        "Aberto" => "Em contagem",
        "Concluido" => "Concluído",
        _ => "Cancelado",
    };

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        // "ids" = inventários selecionados na grade: exporta só eles, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var selecao = ids.Count > 0;
        var lojaId = !selecao && Guid.TryParse(parametros.GetValueOrDefault("lojaId"), out var id) ? id : (Guid?)null;
        var (de, ate) = selecao ? (null, null) : (Data(parametros, "de"), Data(parametros, "ate"));
        var lista = await InventariosEndpoints.FiltrarAsync(lojas, db, selecao ? null : parametros.GetValueOrDefault("situacao"), lojaId, de, ate, ct);
        if (selecao) lista = [.. lista.Where(i => ids.Contains(i.Id))];

        var filtros = new List<FiltroAplicado>();
        if (selecao) filtros.Add(ParametrosDoRelatorio.Selecionados(lista.Count, "inventário", "inventários"));
        else if (de is not null || ate is not null)
            filtros.Add(new("Abertura", $"{de?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "início"} a {ate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "hoje"}"));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Loja", Largura: 1.5f),
            new ColunaRelatorio("Situação", Largura: 0.9f),
            new ColunaRelatorio("Aberto em", TipoColuna.DataHora, Largura: 1f),
            new ColunaRelatorio("Aberto por", Largura: 1.2f),
            new ColunaRelatorio("Encerrado em", TipoColuna.DataHora, Largura: 1f),
            new ColunaRelatorio("SKUs contados", TipoColuna.Inteiro, Largura: 0.7f),
            new ColunaRelatorio("Peças contadas", TipoColuna.Inteiro, Largura: 0.7f),
            new ColunaRelatorio("Observação", Largura: 1.8f),
        ], [.. lista.Select(i => (IReadOnlyList<object?>)
        [
            i.Loja, Situacao(i.Status), i.AbertoEm, i.AbertoPor, i.EncerradoEm, i.SkusContados, i.PecasContadas, i.Observacao,
        ])], filtros);
    }
}
