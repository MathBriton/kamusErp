using Kamus.Modules.Identidade.Aplicacao;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Identidade.Endpoints;

internal static class EmpresasEndpoints
{
    public sealed record EmpresaAtivaResponse(bool Consolidado, IReadOnlyList<EmpresaResumoDto> Empresas);
    public sealed record EmpresaResumoDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj);

    public static IEndpointRouteBuilder MapEmpresas(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/empresas/ativa", EmpresaAtiva)
            .RequireAuthorization()
            .WithTags("Empresas")
            .WithSummary($"Empresa(s) do contexto atual, conforme o header {EmpresaAtivaMiddleware.Header}")
            .Produces<EmpresaAtivaResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        return app;
    }

    private static async Task<IResult> EmpresaAtiva(IEmpresaAtiva empresaAtiva, IdentidadeDbContext db, CancellationToken ct)
    {
        if (!empresaAtiva.Definida)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empresa não informada",
                detail: $"Informe a empresa no header {EmpresaAtivaMiddleware.Header} (id da empresa ou '{EmpresaAtivaMiddleware.Consolidado}').");

        var ids = empresaAtiva.EmpresaIds;
        var empresas = await db.Empresas.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var resposta = empresas
            .OrderBy(e => e.NomeFantasia, StringComparer.CurrentCulture)
            .Select(e => new EmpresaResumoDto(e.Id, e.NomeFantasia, e.RazaoSocial, e.Cnpj.Formatado))
            .ToList();
        return Results.Ok(new EmpresaAtivaResponse(empresaAtiva.Consolidado, resposta));
    }
}
