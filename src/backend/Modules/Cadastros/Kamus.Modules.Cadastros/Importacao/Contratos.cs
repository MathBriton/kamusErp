using Kamus.Infrastructure.Planilhas;

namespace Kamus.Modules.Cadastros.Importacao;

public static class AcaoDaLinha
{
    public const string Criar = "Criar";
    public const string Atualizar = "Atualizar";
    public const string Erro = "Erro";
}

/// <param name="Descricao">Resumo da linha para a prévia (ex.: "Maria Silva — cliente novo").</param>
public sealed record LinhaValidada(int Numero, string Acao, string Descricao, IReadOnlyList<string> Erros);

/// <param name="Criados">Pessoas: linhas que criaram cadastro. Produtos: produtos novos.</param>
/// <param name="Ignorados">Linhas com erro (não importadas).</param>
public sealed record ResultadoDaImportacao(int Criados, int Atualizados, int Ignorados);

/// <summary>Um tipo de planilha importável (clientes, fornecedores, produtos). Validar nunca grava; Importar revalida e grava só as linhas válidas.</summary>
internal interface IImportador
{
    string Tipo { get; }
    string Titulo { get; }
    IReadOnlyCollection<string> Perfis { get; }
    IReadOnlyList<ColunaDoModelo> Colunas { get; }

    Task<IReadOnlyList<LinhaValidada>> ValidarAsync(TabelaLida tabela, CancellationToken ct);

    Task<ResultadoDaImportacao> ImportarAsync(TabelaLida tabela, CancellationToken ct);
}
