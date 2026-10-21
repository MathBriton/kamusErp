namespace Kamus.SharedKernel.Relatorios;

/// <summary>Parâmetros comuns a todos os relatórios exportados das grades.</summary>
public static class ParametrosDoRelatorio
{
    /// <summary>
    /// <c>ids</c> = linhas selecionadas na grade, separadas por vírgula. Quando vem, o relatório exporta só elas
    /// (ignorando os demais filtros). Valores que não são Guid são ignorados.
    /// </summary>
    public static IReadOnlySet<Guid> Ids(IReadOnlyDictionary<string, string?> parametros) =>
        (parametros.GetValueOrDefault("ids") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

    /// <summary>Filtro que aparece no cabeçalho do arquivo: "Selecionados: 3 produtos".</summary>
    public static FiltroAplicado Selecionados(int quantidade, string singular, string plural) =>
        new("Selecionados", $"{quantidade} {(quantidade == 1 ? singular : plural)}");
}
