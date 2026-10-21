using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Relatorios;

/// <summary>Um SKU por loja e linha, só os com saldo. Base para conferência e balanço.</summary>
internal sealed class RelatorioDePosicao(Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo) : IRelatorio
{
    public string Codigo => "posicao-de-estoque";
    public string Titulo => "Posição de estoque";
    public IReadOnlyCollection<string> Perfis => Permissoes.VerCusto;
    public string Area => "Estoque";
    public string? Descricao => "Saldo por produto, cor e tamanho, com custo médio e valor em estoque.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        // "ids" = produtos selecionados na grade: exporta só eles (em todas as lojas visíveis), ignorando os demais filtros.
        var produtos = ParametrosDoRelatorio.Ids(parametros);
        var lojaId = produtos.Count == 0 && Guid.TryParse(parametros.GetValueOrDefault("lojaId"), out var id) ? id : (Guid?)null;
        var busca = produtos.Count == 0 ? parametros.GetValueOrDefault("busca") : null;
        var filtro = await PosicaoEndpoints.LojasDoFiltroAsync(lojas, lojaId, ct) ?? [];
        var nomes = filtro.ToDictionary(l => l.Id, l => l.Nome);
        var ids = nomes.Keys.ToList();
        var consulta = db.Saldos.AsNoTracking().Where(s => ids.Contains(s.LojaId) && s.Quantidade != 0);
        if (produtos.Count > 0)
        {
            var selecionados = produtos.ToList();
            consulta = consulta.Where(s => selecionados.Contains(s.ProdutoId));
        }
        var saldos = await consulta.ToListAsync(ct);
        var skus = (await catalogo.SkusAsync(saldos.Select(s => s.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);

        var linhas = saldos.Where(s => skus.ContainsKey(s.SkuId) && PosicaoEndpoints.Corresponde(skus[s.SkuId], busca))
            .Select(s => (Saldo: s, Sku: skus[s.SkuId]))
            .OrderBy(x => nomes[x.Saldo.LojaId], StringComparer.CurrentCulture).ThenBy(x => x.Sku.Descricao, StringComparer.CurrentCulture)
            .ThenBy(x => x.Sku.Cor, StringComparer.CurrentCulture).ThenBy(x => x.Sku.OrdemTamanho)
            .Select(x => (IReadOnlyList<object?>)
            [
                nomes[x.Saldo.LojaId], x.Sku.Referencia, x.Sku.Descricao, x.Sku.Cor, x.Sku.Tamanho, x.Sku.CodigoDeBarras,
                x.Saldo.Quantidade, x.Saldo.CustoMedio, x.Saldo.ValorEmCusto, x.Saldo.Quantidade * x.Sku.PrecoVenda,
            ]).ToList();

        var filtros = new List<FiltroAplicado>();
        if (produtos.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(saldos.Select(s => s.ProdutoId).Distinct().Count(), "produto", "produtos"));
        if (lojaId is not null && filtro.Count == 1) filtros.Add(new FiltroAplicado("Loja", filtro[0].Nome));
        if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Loja", Largura: 1.2f),
            new ColunaRelatorio("Referência", Largura: 1f),
            new ColunaRelatorio("Descrição", Largura: 2.2f),
            new ColunaRelatorio("Cor", Largura: 0.9f),
            new ColunaRelatorio("Tamanho", Largura: 0.6f),
            new ColunaRelatorio("Código de barras", Largura: 1.3f),
            new ColunaRelatorio("Quantidade", TipoColuna.Inteiro, Totalizar: true, Largura: 0.7f),
            new ColunaRelatorio("Custo médio", TipoColuna.Moeda, Largura: 0.8f),
            new ColunaRelatorio("Valor em custo", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
            new ColunaRelatorio("Valor em venda", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
        ], linhas, filtros);
    }
}
