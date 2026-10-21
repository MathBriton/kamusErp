using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Estoque.Relatorios;

/// <summary>Lojas (locais de estoque) com a situação e o total de peças — os mesmos filtros da tela (busca, situacao) ou só as selecionadas (ids).</summary>
internal sealed class RelatorioDeLojas(Lojas lojas, EstoqueDbContext db) : IRelatorio
{
    public string Codigo => "lojas";
    public string Titulo => "Lojas";
    public IReadOnlyCollection<string> Perfis => Permissoes.VerCusto;
    public string Area => "Estoque";
    public string? Descricao => "Lojas da empresa com a situação e as peças em estoque.";

    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("situacao", "Situação", TipoDeParametro.Opcao, "todas", [new("todas", "Todas"), new("ativas", "Ativas"), new("inativas", "Inativas")]),
    ];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var busca = parametros.GetValueOrDefault("busca");
        var situacao = parametros.GetValueOrDefault("situacao");
        var lista = ids.Count > 0
            ? [.. (await LojasEndpoints.FiltrarAsync(lojas, db, null, null, ct)).Where(l => ids.Contains(l.Id))]
            : await LojasEndpoints.FiltrarAsync(lojas, db, busca, situacao, ct);

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(lista.Count, "loja", "lojas"));
        else
        {
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
            if (situacao is "ativas" or "inativas") filtros.Add(new("Situação", situacao == "ativas" ? "Ativas" : "Inativas"));
        }
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Loja", Largura: 2.5f),
            new ColunaRelatorio("Situação", Largura: 1f),
            new ColunaRelatorio("Peças em estoque", TipoColuna.Inteiro, Totalizar: true, Largura: 1f),
        ], [.. lista.Select(l => (IReadOnlyList<object?>)[l.Nome, l.Ativa ? "Ativa" : "Inativa", l.Quantidade])], filtros);
    }
}
