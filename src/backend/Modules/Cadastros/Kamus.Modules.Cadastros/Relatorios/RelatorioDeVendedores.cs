using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Cadastros.Relatorios;

/// <summary>Vendedores com comissão padrão, login vinculado e situação (mesmos filtros da tela: busca, situacao; ou só os selecionados).</summary>
internal sealed class RelatorioDeVendedores(CadastrosDbContext db, IDiretorioDeClientes diretorio) : IRelatorio
{
    public string Codigo => "vendedores";
    public string Titulo => "Vendedores";
    public IReadOnlyCollection<string> Perfis => Permissoes.Gestao;
    public string Area => "Cadastros";
    public string? Descricao => "Vendedores com comissão padrão, telefone, login vinculado e situação.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("situacao", "Situação", TipoDeParametro.Opcao, "ativos", [new("ativos", "Ativos"), new("inativos", "Inativos"), new("todos", "Todos")])];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var busca = parametros.GetValueOrDefault("busca");
        var situacao = parametros.GetValueOrDefault("situacao") ?? "ativos";
        // "ids" = linhas selecionadas na grade: exporta só elas, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var vendedores = ids.Count > 0
            ? [.. (await VendedoresEndpoints.FiltrarAsync(db, diretorio, null, "todos", ct)).Where(v => ids.Contains(v.Id))]
            : await VendedoresEndpoints.FiltrarAsync(db, diretorio, busca, situacao, ct);

        var linhas = vendedores.Select(v => (IReadOnlyList<object?>)
            [v.Nome, v.ComissaoPercentual, Dominio.Telefone.Formatar(v.Telefone), v.UsuarioNome ?? "Sem login", v.Ativo ? "Ativo" : "Inativo"]).ToList();

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(vendedores.Count, "vendedor", "vendedores"));
        else
        {
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
            filtros.Add(new FiltroAplicado("Situação", situacao switch { "inativos" => "Inativos", "todos" => "Todos", _ => "Ativos" }));
        }

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nome", Largura: 2.2f),
            new ColunaRelatorio("Comissão (%)", TipoColuna.Numero, Largura: 0.9f),
            new ColunaRelatorio("Telefone", Largura: 1.2f),
            new ColunaRelatorio("Login", Largura: 1.6f),
            new ColunaRelatorio("Situação", Largura: 0.8f),
        ], linhas, filtros);
    }
}
