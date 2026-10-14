using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Infrastructure.Auditoria;

public static class AuditoriaEndpoints
{
    private static readonly string[] PerfisComAcesso = ["Dono", "Gerente"];

    public sealed record RegistroDto(Guid Id, DateTimeOffset OcorridoEm, string Tipo, string? Evento, string? Entidade, string? EntidadeId,
        string Descricao, string UsuarioNome, string? Ip, IReadOnlyList<AlteracaoDeCampo> Alteracoes);

    public sealed record PaginaDto(IReadOnlyList<RegistroDto> Itens, int Total, int Pagina, int Tamanho);

    public static IEndpointRouteBuilder MapKamusAuditoria(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auditoria", Consultar)
            .RequireAuthorization()
            .WithTags("Histórico de alterações")
            .WithSummary("Histórico da empresa ativa (e eventos gerais do cliente). Perfis Dono e Gerente.")
            .Produces<PaginaDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        return app;
    }

    private static async Task<IResult> Consultar(
        IEmpresaAtiva empresaAtiva, AuditoriaDbContext db, CancellationToken ct,
        string? entidade = null, string? entidadeId = null, TipoRegistroAuditoria? tipo = null, string? evento = null,
        DateTimeOffset? de = null, DateTimeOffset? ate = null, int pagina = 1, int tamanho = 50)
    {
        if (!empresaAtiva.Definida)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empresa não informada", detail: "Selecione uma empresa (header X-Empresa-Id).");
        if (!PerfisComAcesso.Contains(empresaAtiva.Perfil))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: "O histórico de alterações é restrito aos perfis Dono e Gerente.");

        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 200);
        var empresas = empresaAtiva.EmpresaIds;

        // Registros da(s) empresa(s) ativa(s) + registros gerais do cliente (sem empresa: logins, usuários...).
        var consulta = db.Registros.AsNoTracking().Where(r => r.EmpresaId == null || empresas.Contains(r.EmpresaId.Value));
        if (!string.IsNullOrWhiteSpace(entidade)) consulta = consulta.Where(r => r.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(entidadeId)) consulta = consulta.Where(r => r.EntidadeId == entidadeId);
        if (tipo is not null) consulta = consulta.Where(r => r.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(evento)) consulta = consulta.Where(r => r.Evento == evento);
        if (de is not null) consulta = consulta.Where(r => r.OcorridoEm >= de);
        if (ate is not null) consulta = consulta.Where(r => r.OcorridoEm <= ate);

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(r => r.OcorridoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        var dtos = itens
            .Select(r => new RegistroDto(r.Id, r.OcorridoEm, r.Tipo.ToString(), r.Evento, r.Entidade, r.EntidadeId, r.Descricao, r.UsuarioNome, r.Ip, r.Alteracoes))
            .ToList();
        return Results.Ok(new PaginaDto(dtos, total, pagina, tamanho));
    }
}
