using Kamus.Infrastructure.Planilhas;

namespace Kamus.Infrastructure.Importacao;

public static class AcaoDaLinha
{
    public const string Criar = "Criar";
    public const string Atualizar = "Atualizar";
    public const string Erro = "Erro";
}

/// <param name="Descricao">Resumo da linha para a prévia (ex.: "Maria Silva — cliente novo").</param>
public sealed record LinhaValidada(int Numero, string Acao, string Descricao, IReadOnlyList<string> Erros);

/// <param name="Criados">O que a importação criou (pessoas, produtos novos, saldos...).</param>
/// <param name="Ignorados">Linhas com erro (não importadas).</param>
public sealed record ResultadoDaImportacao(int Criados, int Atualizados, int Ignorados);

/// <summary>Quem importa e em qual empresa: guardado com a planilha, vale também quando a importação roda em segundo plano.</summary>
public sealed record ContextoDaImportacao(Guid UsuarioId, string? UsuarioNome, Guid? EmpresaId, string? EmpresaNome);

/// <summary>
/// Um tipo de planilha importável (clientes, produtos, estoque inicial...). Validar nunca grava; Importar revalida e grava só
/// as linhas válidas. Registre com <c>services.AddScoped&lt;IImportador, ...&gt;()</c>: modelo, prévia, confirmação e segundo plano vêm de graça.
/// </summary>
public interface IImportador
{
    /// <summary>Usado na URL: <c>/api/importacoes/{Tipo}/...</c>.</summary>
    string Tipo { get; }
    string Titulo { get; }

    /// <summary>Tela aberta pelo aviso do sininho quando a importação roda em segundo plano.</summary>
    string Link { get; }
    IReadOnlyCollection<string> Perfis { get; }
    IReadOnlyList<ColunaDoModelo> Colunas { get; }

    Task<IReadOnlyList<LinhaValidada>> ValidarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct);

    Task<ResultadoDaImportacao> ImportarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct);
}
