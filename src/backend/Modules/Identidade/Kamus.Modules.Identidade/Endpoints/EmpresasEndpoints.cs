using System.Security.Claims;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Consultas;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kamus.Modules.Identidade.Endpoints;

internal static class EmpresasEndpoints
{
    public sealed record EmpresaAtivaResponse(bool Consolidado, IReadOnlyList<EmpresaResumoDto> Empresas);
    public sealed record EmpresaResumoDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj);
    public sealed record EmpresaDoClienteDto(Guid Id, string RazaoSocial, string NomeFantasia, string Cnpj);
    public sealed record NovaEmpresaRequest(string? RazaoSocial, string? NomeFantasia, string? Cnpj);
    public sealed record EmpresaRequest(string? RazaoSocial, string? NomeFantasia);

    public static IEndpointRouteBuilder MapEmpresas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/empresas").RequireAuthorization().WithTags("Empresas");

        grupo.MapGet("/ativa", EmpresaAtiva)
            .WithSummary($"Empresa(s) do contexto atual, conforme o header {EmpresaAtivaMiddleware.Header}")
            .Produces<EmpresaAtivaResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        grupo.MapGet("", Listar).WithSummary("Empresas (CNPJs) do cliente — perfil Dono. Filtro: busca (nomes ou CNPJ); com pagina, devolve uma página").Produces<IReadOnlyList<EmpresaDoClienteDto>>();
        grupo.MapPost("", Criar).WithSummary("Cadastrar empresa (CNPJ); quem cadastra vira Dono dela. Respeita o limite do plano")
            .Produces<EmpresaDoClienteDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        grupo.MapPut("/{id:guid}", Editar).WithSummary("Alterar razão social e nome fantasia (o CNPJ não muda)")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
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

    internal static EmpresaDoClienteDto ParaDto(Empresa e) => new(e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj.Formatado);

    /// <summary>Empresas do cliente (por nome fantasia), com a busca da tela: nome fantasia, razão social ou CNPJ.</summary>
    internal static async Task<List<EmpresaDoClienteDto>> FiltrarAsync(IdentidadeDbContext db, string? busca, CancellationToken ct)
    {
        IEnumerable<EmpresaDoClienteDto> empresas = (await db.Empresas.AsNoTracking().ToListAsync(ct))
            .OrderBy(e => e.NomeFantasia, StringComparer.CurrentCulture).Select(ParaDto);
        if (!string.IsNullOrWhiteSpace(busca))
            empresas = empresas.Where(e => TextoParaBusca.Contem(e.NomeFantasia, busca) || TextoParaBusca.Contem(e.RazaoSocial, busca) || TextoParaBusca.ContemDigitos(e.Cnpj, busca));
        return [.. empresas];
    }

    private static async Task<IResult> Listar(IEmpresaAtiva empresaAtiva, IdentidadeDbContext db, CancellationToken ct,
        string? busca = null, int? pagina = null, int tamanho = Paginacao.TamanhoPadrao)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var empresas = await FiltrarAsync(db, busca, ct);
        return pagina is null ? Results.Ok(empresas) : Results.Ok(empresas.Paginar(pagina.Value, tamanho));
    }

    private static async Task<IResult> Criar(
        NovaEmpresaRequest r, IEmpresaAtiva empresaAtiva, ITenantContext tenant, ILimitesDoCliente limites,
        IdentidadeDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        if (string.IsNullOrWhiteSpace(r.RazaoSocial) || string.IsNullOrWhiteSpace(r.NomeFantasia))
            return Gestao.Problema(StatusCodes.Status400BadRequest, "Dados incompletos", "Informe razão social e nome fantasia.");
        if (!Cnpj.TryParse(r.Cnpj, out var cnpj))
            return Gestao.Problema(StatusCodes.Status400BadRequest, "CNPJ inválido", "CNPJ inválido.");
        if (await db.Empresas.AnyAsync(e => e.Cnpj == cnpj, ct))
            return Gestao.Problema(StatusCodes.Status409Conflict, "CNPJ já cadastrado", $"O CNPJ {cnpj.Formatado} já está cadastrado.");

        var limite = (await limites.ObterAsync(tenant.TenantId!.Value, ct)).Cnpjs;
        if (limite is { } max && await db.Empresas.CountAsync(ct) >= max) return Gestao.Limite("CNPJs", max);

        var empresa = Empresa.Criar(r.RazaoSocial, r.NomeFantasia, cnpj);
        db.Empresas.Add(empresa);
        db.AcessosEmpresa.Add(AcessoEmpresa.Criar(Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!), empresa.Id, Perfil.Dono));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/empresas/{empresa.Id}", ParaDto(empresa));
    }

    private static async Task<IResult> Editar(Guid id, EmpresaRequest r, IEmpresaAtiva empresaAtiva, IdentidadeDbContext db, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var empresa = await db.Empresas.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (empresa is null) return Gestao.Problema(StatusCodes.Status404NotFound, "Não encontrada", "Empresa não encontrada.");
        if (string.IsNullOrWhiteSpace(r.RazaoSocial) || string.IsNullOrWhiteSpace(r.NomeFantasia))
            return Gestao.Problema(StatusCodes.Status400BadRequest, "Dados incompletos", "Informe razão social e nome fantasia.");

        empresa.Atualizar(r.RazaoSocial, r.NomeFantasia);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
