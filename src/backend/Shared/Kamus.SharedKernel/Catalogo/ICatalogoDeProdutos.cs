namespace Kamus.SharedKernel.Catalogo;

/// <param name="PrecoVenda">Preço efetivo (o do SKU, se houver, senão o do produto).</param>
public sealed record SkuDoCatalogo(
    Guid Id, Guid ProdutoId, string Referencia, string Descricao, Guid CorId, string Cor, string? CorHex,
    string Tamanho, int OrdemTamanho, string CodigoDeBarras, decimal PrecoVenda, decimal? PrecoCusto, bool Ativo);

public sealed record CorDoCatalogo(Guid Id, string Nome, string? Hex);

/// <param name="Tamanhos">Na ordem da grade (colunas da matriz).</param>
/// <param name="Cores">Na ordem do cadastro do produto (linhas da matriz).</param>
public sealed record ProdutoDoCatalogo(
    Guid Id, string Referencia, string Descricao, string? Marca, string? Categoria, decimal PrecoVenda, decimal? PrecoCusto, bool Ativo,
    IReadOnlyList<string> Tamanhos, IReadOnlyList<CorDoCatalogo> Cores, IReadOnlyList<SkuDoCatalogo> Skus);

/// <summary>Leitura do catálogo (produtos, cores e SKUs) do cliente atual para outros módulos. Implementado pelo módulo Cadastros.</summary>
public interface ICatalogoDeProdutos
{
    Task<ProdutoDoCatalogo?> ProdutoAsync(Guid produtoId, CancellationToken ct = default);

    /// <summary>SKUs pelos ids (os inexistentes são omitidos).</summary>
    Task<IReadOnlyList<SkuDoCatalogo>> SkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);

    /// <summary>SKU pelo código de barras (leitor). Nulo se não existe.</summary>
    Task<SkuDoCatalogo?> SkuPorCodigoAsync(string codigoDeBarras, CancellationToken ct = default);

    /// <summary>Todos os SKUs do cliente — para casar planilhas por referência/cor/tamanho ou código de barras.</summary>
    Task<IReadOnlyList<SkuDoCatalogo>> TodosOsSkusAsync(CancellationToken ct = default);
}
