using Kamus.SharedKernel.Consultas;

namespace Kamus.Modules.Vendas.Aplicacao;

internal static class Paginas
{
    /// <summary>
    /// Página pedida pela grade do web; sem <paramref name="pagina"/>, devolve todas as linhas numa página só
    /// (outros chamadores, como o app e o histórico do cliente, dependem da lista completa).
    /// </summary>
    public static PaginaDeRegistros<T> PaginarSePedido<T>(this IEnumerable<T> todos, int? pagina, int? tamanho)
    {
        var lista = todos as IReadOnlyList<T> ?? todos.ToList();
        return pagina is null ? new PaginaDeRegistros<T>(lista, lista.Count, 1, lista.Count) : lista.Paginar(pagina.Value, tamanho ?? Paginacao.TamanhoPadrao);
    }
}
