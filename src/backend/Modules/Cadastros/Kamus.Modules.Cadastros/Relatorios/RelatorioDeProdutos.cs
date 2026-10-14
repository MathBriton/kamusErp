using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Relatorios;

/// <summary>Um SKU por linha (produto, cor, tamanho, código de barras e preço) — base para conferência e etiquetas.</summary>
internal sealed class RelatorioDeProdutos(CadastrosDbContext db) : IRelatorio
{
    public string Codigo => "produtos";
    public string Titulo => "Produtos e SKUs";
    public IReadOnlyCollection<string> Perfis => ["Dono", "Gerente", "Estoquista", "Vendedor", "Financeiro"];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var busca = parametros.GetValueOrDefault("busca");
        var situacao = parametros.GetValueOrDefault("situacao");
        var produtos = await ProdutosEndpoints.FiltrarAsync(db, busca, parametros.GetValueOrDefault("categoria"), parametros.GetValueOrDefault("colecao"), situacao, ct);
        var cores = await db.Cores.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        var linhas = produtos.SelectMany(p => p.Skus.Where(s => s.Ativo).OrderBy(s => cores.GetValueOrDefault(s.CorId)).ThenBy(s => s.OrdemTamanho)
            .Select(s => (IReadOnlyList<object?>)
            [
                p.Referencia, p.Descricao, cores.GetValueOrDefault(s.CorId, "?"), s.Tamanho, s.CodigoDeBarras, p.Categoria, p.Colecao,
                p.PrecoCusto, p.PrecoDe(s), p.Markup,
            ])).ToList();

        var filtros = new List<FiltroAplicado>();
        if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Referência", Largura: 1f),
            new ColunaRelatorio("Descrição", Largura: 2.2f),
            new ColunaRelatorio("Cor", Largura: 0.9f),
            new ColunaRelatorio("Tamanho", Largura: 0.6f),
            new ColunaRelatorio("Código de barras", Largura: 1.3f),
            new ColunaRelatorio("Categoria", Largura: 1f),
            new ColunaRelatorio("Coleção", Largura: 1f),
            new ColunaRelatorio("Custo", TipoColuna.Moeda, Largura: 0.8f),
            new ColunaRelatorio("Preço", TipoColuna.Moeda, Largura: 0.8f),
            new ColunaRelatorio("Markup", TipoColuna.Numero, Largura: 0.6f),
        ], linhas, filtros);
    }
}
