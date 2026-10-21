using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Relatorios;

/// <summary>Grades de tamanho (nome, tamanhos na ordem e se já têm produtos). Filtro: busca; ou só as selecionadas (ids).</summary>
internal sealed class RelatorioDeGrades(CadastrosDbContext db) : IRelatorio
{
    public string Codigo => "grades";
    public string Titulo => "Grades de tamanho";
    public IReadOnlyCollection<string> Perfis => Permissoes.Catalogo;
    public string Area => "Cadastros";
    public string? Descricao => "Grades de tamanho com os tamanhos na ordem da etiqueta.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var busca = parametros.GetValueOrDefault("busca");
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var emUso = await db.Produtos.Select(p => p.GradeId).Distinct().ToListAsync(ct);
        IEnumerable<GradeDeTamanhos> grades = await db.Grades.AsNoTracking().ToListAsync(ct);
        if (ids.Count > 0) grades = grades.Where(g => ids.Contains(g.Id));
        else if (!string.IsNullOrWhiteSpace(busca)) grades = grades.Where(g => TextoParaBusca.Contem(g.Nome, busca));
        var lista = grades.OrderBy(g => g.Nome, StringComparer.CurrentCulture).ToList();

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(lista.Count, "grade", "grades"));
        else if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Grade", Largura: 1.6f),
            new ColunaRelatorio("Tamanhos", Largura: 3f),
            new ColunaRelatorio("Em uso", Largura: 0.7f),
        ], lista.Select(g => (IReadOnlyList<object?>)[g.Nome, string.Join(", ", g.Tamanhos), emUso.Contains(g.Id) ? "Sim" : "Não"]).ToList(), filtros);
    }
}

/// <summary>Cores dos produtos (nome e código hexadecimal da amostra). Filtro: busca; ou só as selecionadas (ids).</summary>
internal sealed class RelatorioDeCores(CadastrosDbContext db) : IRelatorio
{
    public string Codigo => "cores";
    public string Titulo => "Cores";
    public IReadOnlyCollection<string> Perfis => Permissoes.Catalogo;
    public string Area => "Cadastros";
    public string? Descricao => "Cores cadastradas com o código da amostra.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var busca = parametros.GetValueOrDefault("busca");
        var ids = ParametrosDoRelatorio.Ids(parametros);
        IEnumerable<Cor> cores = await db.Cores.AsNoTracking().ToListAsync(ct);
        if (ids.Count > 0) cores = cores.Where(c => ids.Contains(c.Id));
        else if (!string.IsNullOrWhiteSpace(busca)) cores = cores.Where(c => TextoParaBusca.Contem(c.Nome, busca));
        var lista = cores.OrderBy(c => c.Nome, StringComparer.CurrentCulture).ToList();

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(lista.Count, "cor", "cores"));
        else if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Cor", Largura: 2f),
            new ColunaRelatorio("Amostra (hex)", Largura: 1f),
        ], lista.Select(c => (IReadOnlyList<object?>)[c.Nome, c.Hex]).ToList(), filtros);
    }
}
