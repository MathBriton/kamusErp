using Kamus.SharedKernel.Busca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Infrastructure.Busca;

public static class BuscaEndpoints
{
    public const int TamanhoMinimoDoTermo = 2;
    private const int LimitePorProvedor = 5;

    public sealed record ItemDto(string Tipo, string Id, string Titulo, string? Subtitulo, string? Link);
    public sealed record GrupoDto(string Grupo, IReadOnlyList<ItemDto> Itens);
    public sealed record RespostaDto(string Termo, IReadOnlyList<GrupoDto> Grupos);

    public static IEndpointRouteBuilder MapKamusBusca(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/busca", Buscar)
            .RequireAuthorization()
            .WithTags("Pesquisa global")
            .WithSummary($"Pesquisa em todos os módulos (mínimo de {TamanhoMinimoDoTermo} caracteres, ignora acentos)")
            .Produces<RespostaDto>();
        return app;
    }

    private static async Task<IResult> Buscar(string? q, IEnumerable<IProvedorDeBusca> provedores, CancellationToken ct)
    {
        var termo = (q ?? "").Trim();
        if (termo.Length < TamanhoMinimoDoTermo)
            return Results.Ok(new RespostaDto(termo, []));

        var resultados = new List<ResultadoDeBusca>();
        foreach (var provedor in provedores)
            resultados.AddRange(await provedor.BuscarAsync(termo, LimitePorProvedor, ct));

        var grupos = resultados
            .GroupBy(r => r.Grupo)
            .Select(g => new GrupoDto(g.Key, g.Select(r => new ItemDto(r.Tipo, r.Id, r.Titulo, r.Subtitulo, r.Link)).ToList()))
            .ToList();
        return Results.Ok(new RespostaDto(termo, grupos));
    }
}
