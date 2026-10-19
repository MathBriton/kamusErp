namespace Kamus.SharedKernel.Indicadores;

/// <param name="Esgotados">Cor e tamanho que já tiveram estoque e zeraram (ex.: "Azul M").</param>
public sealed record QuebraDeGrade(Guid ProdutoId, string Descricao, IReadOnlyList<string> Esgotados);

/// <param name="ValorDeCusto">Peças × custo médio.</param>
/// <param name="ValorDeVenda">Peças × preço de venda atual.</param>
/// <param name="Quebras">Até 5 produtos com estoque em parte da grade e algum SKU esgotado.</param>
public sealed record ResumoDeEstoque(int Pecas, decimal ValorDeCusto, decimal ValorDeVenda, int SkusNegativos, IReadOnlyList<QuebraDeGrade> Quebras, int TotalDeQuebras);

/// <summary>Números de estoque para o dashboard e o resumo diário (somando as lojas das empresas). Implementado pelo módulo Estoque.</summary>
public interface IIndicadoresDeEstoque
{
    Task<ResumoDeEstoque> ResumoAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default);
}
