using Kamus.Modules.Identidade.Endpoints;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Identidade.Relatorios;

/// <summary>Usuários do cliente com os acessos por empresa (nunca senhas). Mesmos filtros da tela ou só os selecionados (ids).</summary>
internal sealed class RelatorioDeUsuarios(IdentidadeDbContext db, ITenantContext tenant) : IRelatorio
{
    public string Codigo => "usuarios";
    public string Titulo => "Usuários";
    public IReadOnlyCollection<string> Perfis => ["Dono"];
    public string Area => "Sistema";
    public string? Descricao => "Usuários com o perfil em cada empresa, a situação e o último acesso.";

    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
    [
        new("situacao", "Situação", TipoDeParametro.Opcao, "todos", [new("todos", "Todos"), new("ativos", "Ativos"), new("inativos", "Inativos")]),
    ];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        // "ids" = usuários selecionados na grade: exporta só eles, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var busca = parametros.GetValueOrDefault("busca");
        var empresaId = Guid.TryParse(parametros.GetValueOrDefault("empresaId"), out var e) ? e : (Guid?)null;
        var usuarios = ids.Count > 0
            ? [.. (await UsuariosEndpoints.FiltrarAsync(db, tenant, null, null, null, null, ct)).Where(u => ids.Contains(u.Id))]
            : await UsuariosEndpoints.FiltrarAsync(db, tenant, busca, parametros.GetValueOrDefault("situacao"), empresaId, parametros.GetValueOrDefault("perfil"), ct);

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(usuarios.Count, "usuário", "usuários"));
        else
        {
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
            if (parametros.GetValueOrDefault("perfil") is { Length: > 0 } perfil) filtros.Add(new("Perfil", perfil));
        }
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nome", Largura: 1.6f),
            new ColunaRelatorio("E-mail", Largura: 1.8f),
            new ColunaRelatorio("Acessos", Largura: 2.6f),
            new ColunaRelatorio("Situação", Largura: 1.1f),
            new ColunaRelatorio("Último acesso", TipoColuna.DataHora, Largura: 1f),
        ], [.. usuarios.Select(u => (IReadOnlyList<object?>)
        [
            u.Nome, u.Email, string.Join("; ", u.Acessos.Select(a => $"{a.Empresa}: {a.Perfil}")),
            !u.Ativo ? "Inativo" : u.DeveTrocarSenha ? "Aguardando 1º acesso" : "Ativo", u.UltimoLoginEm,
        ])], filtros);
    }
}

/// <summary>Empresas (CNPJs) do cliente. Busca da tela ou só as selecionadas (ids).</summary>
internal sealed class RelatorioDeEmpresas(IdentidadeDbContext db) : IRelatorio
{
    public string Codigo => "empresas";
    public string Titulo => "Empresas";
    public IReadOnlyCollection<string> Perfis => ["Dono"];
    public string Area => "Sistema";
    public string? Descricao => "Os CNPJs do negócio com razão social e nome fantasia.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var busca = parametros.GetValueOrDefault("busca");
        var empresas = ids.Count > 0
            ? [.. (await EmpresasEndpoints.FiltrarAsync(db, null, ct)).Where(e => ids.Contains(e.Id))]
            : await EmpresasEndpoints.FiltrarAsync(db, busca, ct);

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(empresas.Count, "empresa", "empresas"));
        else if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nome fantasia", Largura: 1.8f),
            new ColunaRelatorio("Razão social", Largura: 2.4f),
            new ColunaRelatorio("CNPJ", Largura: 1.2f),
        ], [.. empresas.Select(e => (IReadOnlyList<object?>)[e.NomeFantasia, e.RazaoSocial, e.Cnpj])], filtros);
    }
}
