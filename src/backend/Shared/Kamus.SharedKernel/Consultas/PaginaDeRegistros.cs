namespace Kamus.SharedKernel.Consultas;

/// <summary>Uma página de uma lista (grade padrão do web): os itens da página e o total de registros com os filtros.</summary>
public sealed record PaginaDeRegistros<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int Tamanho)
{
    /// <summary>Converte só os itens da página (pagine as entidades primeiro e monte os DTOs depois).</summary>
    public PaginaDeRegistros<TDestino> Converter<TDestino>(Func<T, TDestino> conversao) => new(Itens.Select(conversao).ToList(), Total, Pagina, Tamanho);
}

public static class Paginacao
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 200;

    /// <summary>Recorta a página de uma lista já filtrada e ordenada (página mínima 1, tamanho de 1 a 200).</summary>
    public static PaginaDeRegistros<T> Paginar<T>(this IEnumerable<T> todos, int pagina, int tamanho)
    {
        var lista = todos as IReadOnlyList<T> ?? todos.ToList();
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, TamanhoMaximo);
        return new PaginaDeRegistros<T>(lista.Skip((pagina - 1) * tamanho).Take(tamanho).ToList(), lista.Count, pagina, tamanho);
    }
}

/// <summary>Inativar ou reativar vários registros de uma vez (seleção da grade).</summary>
public sealed record SituacaoEmLoteRequest(IReadOnlyList<Guid>? Ids, bool Ativo);

public sealed record SituacaoEmLoteResponse(int Alterados);
