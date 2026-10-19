namespace Kamus.SharedKernel.Relatorios;

public enum TipoColuna
{
    Texto,
    Inteiro,
    Numero,
    Moeda,
    Data,
    DataHora,
}

/// <param name="Totalizar">Soma a coluna na linha "Total" (PDF e Excel).</param>
/// <param name="Largura">Largura relativa no PDF (padrão: 2 para texto, 1 para os demais).</param>
public sealed record ColunaRelatorio(string Titulo, TipoColuna Tipo = TipoColuna.Texto, bool Totalizar = false, float? Largura = null);

public sealed record FiltroAplicado(string Nome, string Valor);

/// <summary>
/// Definição única de um relatório: a tela e todas as exportações (PDF, Excel, CSV) usam os mesmos dados.
/// Valores das linhas: string, números (int/long/decimal/double), DateOnly, DateTime, DateTimeOffset ou null.
/// </summary>
public sealed record DadosDoRelatorio(
    string Titulo,
    IReadOnlyList<ColunaRelatorio> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    IReadOnlyList<FiltroAplicado> Filtros);

/// <summary>Periodo = par de/ate (padrão: "mes", "hoje" ou "30dias"); Opcao = lista fechada; Inteiro e Data = campo simples.</summary>
public enum TipoDeParametro
{
    Periodo,
    Data,
    Inteiro,
    Opcao,
}

public sealed record OpcaoDoParametro(string Valor, string Rotulo);

/// <param name="Nome">Nome na query string ("periodo" vira <c>de</c> e <c>ate</c>).</param>
public sealed record ParametroDoRelatorio(string Nome, string Rotulo, TipoDeParametro Tipo, string? Padrao = null, IReadOnlyList<OpcaoDoParametro>? Opcoes = null);

/// <summary>Relatório exportável. Cada módulo registra os seus; o endpoint de exportação é único.</summary>
public interface IRelatorio
{
    /// <summary>Usado na URL: <c>/api/relatorios/{codigo}.{formato}</c>.</summary>
    string Codigo { get; }
    string Titulo { get; }

    /// <summary>Perfis (na empresa ativa) que podem exportar.</summary>
    IReadOnlyCollection<string> Perfis { get; }

    /// <param name="parametros">Filtros da tela, vindos da query string.</param>
    Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct);

    /// <summary>Grupo na central de relatórios (Vendas, Estoque, Financeiro, Cadastros, Gestão, Sistema).</summary>
    string Area => "Outros";

    string? Descricao => null;

    /// <summary>Falso para relatórios que dependem de uma tela (ex.: um inventário específico): ficam fora da central.</summary>
    bool NaCentral => true;

    /// <summary>Filtros que a central de relatórios mostra para este relatório.</summary>
    IReadOnlyList<ParametroDoRelatorio> Parametros => [];
}
