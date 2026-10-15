using Kamus.Modules.Financeiro.Aplicacao;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Modules.Financeiro.Endpoints;

internal static class FluxoEndpoints
{
    public static IEndpointRouteBuilder MapFluxo(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/financeiro").RequireAuthorization().WithTags("Financeiro")
            .MapGet("/fluxo", Fluxo)
            .WithSummary("Fluxo de caixa: realizado até hoje, previsto depois, em atraso à parte. Filtros: de, ate (padrão: próximos 30 dias), contaId. Acima de 62 dias agrupa por mês");
        return app;
    }

    private static async Task<IResult> Fluxo(DateOnly? de, DateOnly? ate, Guid? contaId, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var inicio = de ?? hoje;
        var fim = ate ?? inicio.AddDays(30);
        if (fim < inicio) return Permissoes.Erro("A data final precisa ser depois da inicial.");
        if (fim.DayNumber - inicio.DayNumber > 731) return Permissoes.Erro("Período máximo de 2 anos.");
        return Results.Ok(await Consultas.FluxoAsync(db, empresa, inicio, fim, contaId, hoje, ct));
    }
}
