using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Endpoints;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Financeiro.Relatorios;

/// <summary>Plano de contas (categorias de receita e despesa) com os filtros da tela, ou só as selecionadas.</summary>
internal sealed class RelatorioDeCategorias(FinanceiroDbContext db) : IRelatorio
{
    public string Codigo => "categorias-financeiras";
    public string Titulo => "Categorias financeiras";
    public IReadOnlyCollection<string> Perfis => Permissoes.Financeiro;
    public string Area => "Financeiro";
    public string? Descricao => "Plano de contas: grupos e categorias de receitas e despesas, com a situação.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("tipo", "Tipo", TipoDeParametro.Opcao, "todos", [new("todos", "Receitas e despesas"), new("Receita", "Receitas"), new("Despesa", "Despesas")]),
        new("situacao", "Situação", TipoDeParametro.Opcao, "ativas", [new("ativas", "Ativas"), new("inativas", "Inativas"), new("todas", "Todas")]),
    ];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        // "ids" = linhas selecionadas na grade: exporta só elas, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var filtros = new List<FiltroAplicado>();
        List<Categoria> categorias;
        if (ids.Count > 0)
        {
            categorias = [.. (await CategoriasEndpoints.FiltrarAsync(db, null, "todas", null, ct)).Where(c => ids.Contains(c.Id))];
            filtros.Add(ParametrosDoRelatorio.Selecionados(categorias.Count, "categoria", "categorias"));
        }
        else
        {
            var tipo = CategoriasEndpoints.TentarTipo(parametros.GetValueOrDefault("tipo"), out var t) ? t : (TipoDeCategoria?)null;
            var situacao = parametros.GetValueOrDefault("situacao") ?? "todas";
            var busca = parametros.GetValueOrDefault("busca");
            categorias = await CategoriasEndpoints.FiltrarAsync(db, tipo, situacao, busca, ct);
            if (tipo is not null) filtros.Add(new("Tipo", tipo.ToString()!));
            filtros.Add(new("Situação", situacao));
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        }

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Tipo", Largura: 0.8f),
            new ColunaRelatorio("Grupo", Largura: 1.4f),
            new ColunaRelatorio("Categoria", Largura: 2f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], categorias.Select(c => (IReadOnlyList<object?>)[c.Tipo.ToString(), c.Grupo, c.Nome, c.Ativa ? "Ativa" : "Inativa"]).ToList(), filtros);
    }
}

/// <summary>Contas financeiras (caixa, bancos, carteiras) da empresa ativa com o saldo atual, ou só as selecionadas.</summary>
internal sealed class RelatorioDeContasFinanceiras(FinanceiroDbContext db, IEmpresaAtiva empresa) : IRelatorio
{
    public string Codigo => "contas-financeiras";
    public string Titulo => "Contas financeiras";
    public IReadOnlyCollection<string> Perfis => Permissoes.Financeiro;
    public string Area => "Financeiro";
    public string? Descricao => "Caixa, contas bancárias e carteiras com saldo inicial e saldo atual.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("situacao", "Situação", TipoDeParametro.Opcao, "ativas", [new("ativas", "Ativas"), new("inativas", "Inativas"), new("todas", "Todas")]),
    ];

    private static readonly Dictionary<string, string> Tipos = new() { ["Caixa"] = "Caixa da loja", ["Banco"] = "Conta bancária", ["Carteira"] = "Carteira digital" };

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var filtros = new List<FiltroAplicado>();
        List<ContasEndpoints.ContaDto> contas;
        if (ids.Count > 0)
        {
            contas = [.. (await ContasEndpoints.FiltrarAsync(db, empresa, null, "todas", null, ct)).Where(c => ids.Contains(c.Id))];
            filtros.Add(ParametrosDoRelatorio.Selecionados(contas.Count, "conta", "contas"));
        }
        else
        {
            var tipo = ContasEndpoints.TentarTipo(parametros.GetValueOrDefault("tipo"), out var t) ? t : (TipoDeConta?)null;
            var situacao = parametros.GetValueOrDefault("situacao") ?? "todas";
            var busca = parametros.GetValueOrDefault("busca");
            contas = await ContasEndpoints.FiltrarAsync(db, empresa, tipo, situacao, busca, ct);
            if (tipo is not null) filtros.Add(new("Tipo", Tipos[tipo.ToString()!]));
            filtros.Add(new("Situação", situacao));
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        }

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Conta", Largura: 2f),
            new ColunaRelatorio("Tipo", Largura: 1.1f),
            new ColunaRelatorio("Saldo inicial", TipoColuna.Moeda, Largura: 0.9f),
            new ColunaRelatorio("Desde", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Saldo atual", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], contas.Select(c => (IReadOnlyList<object?>)[c.Nome, Tipos.GetValueOrDefault(c.Tipo, c.Tipo), c.SaldoInicial, c.SaldoInicialEm, c.Saldo, c.Ativa ? "Ativa" : "Inativa"])
            .ToList(), filtros);
    }
}
