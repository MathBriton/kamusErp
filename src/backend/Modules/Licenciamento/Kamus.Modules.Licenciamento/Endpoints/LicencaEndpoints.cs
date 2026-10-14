using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Licenciamento.Endpoints;

internal static class LicencaEndpoints
{
    public sealed record PagamentoDto(Guid Id, decimal Valor, int Meses, DateOnly PagoEm, DateOnly EstendeuAte, string? Forma);

    public static IEndpointRouteBuilder MapLicenca(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/licenca").RequireAuthorization().WithTags("Licença");

        grupo.MapGet("", Situacao).WithSummary("Situação da licença do cliente (funciona mesmo com a licença suspensa)")
            .Produces<SituacaoDaLicenca>().ProducesProblem(StatusCodes.Status404NotFound);
        grupo.MapGet("/pagamentos", Pagamentos).WithSummary("Mensalidades registradas (perfil Dono)")
            .Produces<IReadOnlyList<PagamentoDto>>().ProducesProblem(StatusCodes.Status403Forbidden);
        return app;
    }

    private static async Task<IResult> Situacao(ITenantContext tenant, LicenciamentoDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var licenca = await db.Licencas.AsNoTracking().SingleOrDefaultAsync(l => l.TenantId == tenant.TenantId, ct);
        return licenca is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Sem licença", detail: "Nenhuma licença cadastrada para este cliente.")
            : Results.Ok(SituacaoDaLicenca.De(licenca, CalendarioBrasilia.Hoje(relogio)));
    }

    private static async Task<IResult> Pagamentos(ITenantContext tenant, IEmpresaAtiva empresa, LicenciamentoDbContext db, CancellationToken ct)
    {
        if (empresa.Perfil != "Dono")
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: "Os pagamentos da licença são visíveis só para o perfil Dono.");

        var pagamentos = await db.Pagamentos.AsNoTracking()
            .Where(p => p.TenantId == tenant.TenantId)
            .OrderByDescending(p => p.PagoEm).ThenByDescending(p => p.RegistradoEm)
            .Select(p => new PagamentoDto(p.Id, p.Valor, p.Meses, p.PagoEm, p.EstendeuAte, p.Forma))
            .ToListAsync(ct);
        return Results.Ok(pagamentos);
    }
}
