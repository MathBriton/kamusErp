namespace Kamus.SharedKernel.Indicadores;

public sealed record VendasDoDia(DateOnly Data, int Vendas, int Pecas, decimal Total);

public sealed record TotalPorNome(string Nome, int Quantidade, decimal Total);

/// <param name="Descricao">Produto, cor e tamanho (como saiu na venda).</param>
public sealed record MaisVendido(Guid SkuId, string Descricao, int Pecas, decimal Total);

/// <param name="PorDia">Todos os dias do período, inclusive os sem venda.</param>
/// <param name="PorForma">Valor recebido por forma de pagamento (sem o troco).</param>
public sealed record ResumoDeVendas(int Vendas, int Pecas, decimal Total, decimal TicketMedio, IReadOnlyList<VendasDoDia> PorDia,
    IReadOnlyList<TotalPorNome> PorForma, IReadOnlyList<TotalPorNome> PorVendedor, IReadOnlyList<MaisVendido> MaisVendidos);

public sealed record CondicionaisNaRua(int Abertas, int Atrasadas, int Pecas, decimal Valor);

/// <summary>Números de vendas para o dashboard e o resumo diário (só vendas concluídas). Implementado pelo módulo Vendas.</summary>
public interface IIndicadoresDeVendas
{
    /// <param name="registradasPor">Só as vendas registradas por este usuário (visão do vendedor).</param>
    Task<ResumoDeVendas> VendasAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly de, DateOnly ate, Guid? registradasPor = null, CancellationToken ct = default);

    Task<CondicionaisNaRua> CondicionaisAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly hoje, CancellationToken ct = default);
}
