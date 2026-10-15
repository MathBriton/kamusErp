using Kamus.Infrastructure.Etiquetas;
using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>Etiquetas em PDF (A4 3 × 10 ou térmica 40 × 25 mm): por SKU e quantidade, ou as peças que entraram numa operação.</summary>
internal static class EtiquetasEndpoints
{
    public sealed record ItemDeEtiqueta(Guid SkuId, int Quantidade);
    public sealed record EtiquetasRequest(string? Formato, IReadOnlyList<ItemDeEtiqueta>? Itens);

    public static IEndpointRouteBuilder MapEtiquetas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque/etiquetas").RequireAuthorization().WithTags("Estoque");
        g.MapPost("/", PorItens).WithSummary("PDF de etiquetas por SKU e quantidade (formato: a4 ou termica)")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(StatusCodes.Status400BadRequest);
        g.MapGet("/operacao/{operacaoId:guid}", DaOperacao).WithSummary("PDF com uma etiqueta por peça que entrou na operação (entrada, nota, transferência)")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");
        return app;
    }

    private static IResult Erro(string detalhe) => Permissoes.Problema(StatusCodes.Status400BadRequest, detalhe);

    private static async Task<IResult> GerarAsync(IReadOnlyList<(Guid SkuId, int Quantidade)> itens, string? formatoTexto, ICatalogoDeProdutos catalogo, CancellationToken ct)
    {
        if (!GeradorDeEtiquetas.TentarFormato(formatoTexto, out var formato)) return Erro("Formato de etiqueta desconhecido. Use a4 ou termica.");
        if (itens.Count == 0 || itens.Any(i => i.Quantidade <= 0)) return Erro("Informe ao menos um SKU com quantidade maior que zero.");
        if (itens.Sum(i => (long)i.Quantidade) > GeradorDeEtiquetas.Maximo) return Erro($"No máximo {GeradorDeEtiquetas.Maximo} etiquetas por vez.");
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (itens.Any(i => !skus.ContainsKey(i.SkuId))) return Erro("Há itens que não existem no catálogo. Atualize a tela e tente de novo.");

        var etiquetas = itens.SelectMany(i =>
        {
            var s = skus[i.SkuId];
            return Enumerable.Repeat(new Etiqueta(s.Descricao, $"{s.Referencia} · {s.Cor} · {s.Tamanho}", s.PrecoVenda, s.CodigoDeBarras), i.Quantidade);
        }).ToList();
        return Results.File(GeradorDeEtiquetas.Gerar(etiquetas, formato), "application/pdf", "etiquetas.pdf");
    }

    private static async Task<IResult> PorItens(EtiquetasRequest r, ICatalogoDeProdutos catalogo, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (!Permissoes.PodeMovimentar(empresa.Perfil)) return Permissoes.Negado("Só Dono, Gerente e Estoquista imprimem etiquetas.");
        var itens = (r.Itens ?? []).GroupBy(i => i.SkuId).Select(g => (g.Key, g.Sum(i => i.Quantidade))).ToList();
        return await GerarAsync(itens, r.Formato, catalogo, ct);
    }

    private static async Task<IResult> DaOperacao(Guid operacaoId, string? formato, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (!Permissoes.PodeMovimentar(empresa.Perfil)) return Permissoes.Negado("Só Dono, Gerente e Estoquista imprimem etiquetas.");
        var ids = (await lojas.VisiveisAsync(ct)).Select(l => l.Id).ToList();
        var movimentos = await db.Movimentos.AsNoTracking()
            .Where(m => m.OperacaoId == operacaoId && m.Quantidade > 0 && ids.Contains(m.LojaId))
            .OrderBy(m => m.Sequencia).ToListAsync(ct);
        if (movimentos.Count == 0) return Results.NotFound();
        return await GerarAsync(movimentos.Select(m => (m.SkuId, m.Quantidade)).ToList(), formato, catalogo, ct);
    }
}
