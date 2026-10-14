using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>Lojas (locais de estoque) da empresa ativa. Só o Dono cria, renomeia, desativa e reativa (contam no limite do plano).</summary>
internal static class LojasEndpoints
{
    public sealed record LojaRequest(string? Nome);
    public sealed record LojaDto(Guid Id, Guid EmpresaId, string Nome, bool Ativa, int Quantidade);

    public static IEndpointRouteBuilder MapLojas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque/lojas").RequireAuthorization().WithTags("Estoque");
        g.MapGet("/", Listar).WithSummary("Lojas da empresa ativa com o total de peças (a primeira é criada no primeiro acesso)");
        g.MapPost("/", Criar).WithSummary("Nova loja (Dono; respeita o limite de lojas do plano)").Produces<LojaDto>(StatusCodes.Status201Created);
        g.MapPut("/{id:guid}", Renomear).WithSummary("Renomeia a loja (Dono)");
        g.MapPost("/{id:guid}/desativar", Desativar).WithSummary("Desativa a loja (precisa estar sem estoque e não ser a última ativa)");
        g.MapPost("/{id:guid}/reativar", Reativar).WithSummary("Reativa a loja (respeita o limite do plano)");
        return app;
    }

    private static IResult? ExigirDono(IEmpresaAtiva empresa) =>
        empresa.Perfil == "Dono" && !empresa.Consolidado ? null : Permissoes.Negado("Somente o perfil Dono gerencia as lojas.");

    private static async Task<List<LojaDto>> ComQuantidadesAsync(EstoqueDbContext db, List<Loja> lojas, CancellationToken ct)
    {
        var ids = lojas.Select(l => l.Id).ToList();
        var totais = await db.Saldos.Where(s => ids.Contains(s.LojaId)).GroupBy(s => s.LojaId)
            .Select(x => new { x.Key, Total = x.Sum(s => s.Quantidade) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return lojas.Select(l => new LojaDto(l.Id, l.EmpresaId, l.Nome, l.Ativa, totais.GetValueOrDefault(l.Id))).ToList();
    }

    private static async Task<IResult> Listar(Lojas lojas, EstoqueDbContext db, CancellationToken ct) =>
        Results.Ok(await ComQuantidadesAsync(db, await lojas.VisiveisAsync(ct), ct));

    private static async Task<IResult?> AcimaDoLimiteAsync(EstoqueDbContext db, ILimitesDoCliente limites, ITenantContext tenant, CancellationToken ct)
    {
        var limite = (await limites.ObterAsync(tenant.TenantId!.Value, ct)).Lojas;
        if (limite is null || await db.Lojas.CountAsync(l => l.Ativa, ct) < limite) return null;
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Limite do plano",
            detail: $"Seu plano permite até {limite} {(limite == 1 ? "loja" : "lojas")}. Fale com o suporte para ampliar.",
            extensions: new Dictionary<string, object?> { ["codigo"] = "LIMITE_DA_LICENCA" });
    }

    private static IResult? NomeRepetido(List<Loja> daEmpresa, string nome, Guid? exceto)
    {
        var chave = TextoParaBusca.Normalizar(nome);
        return daEmpresa.Any(l => l.Id != exceto && TextoParaBusca.Normalizar(l.Nome) == chave)
            ? Permissoes.Problema(StatusCodes.Status409Conflict, $"Já existe uma loja chamada {nome.Trim()} nesta empresa.")
            : null;
    }

    private static async Task<IResult> Criar(LojaRequest r, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, ILimitesDoCliente limites,
        ITenantContext tenant, TimeProvider relogio, CancellationToken ct)
    {
        if (ExigirDono(empresa) is { } negado) return negado;
        if (string.IsNullOrWhiteSpace(r.Nome)) return Permissoes.Problema(StatusCodes.Status400BadRequest, "Informe o nome da loja.");
        var daEmpresa = await lojas.VisiveisAsync(ct);
        if (NomeRepetido(daEmpresa, r.Nome, null) is { } repetido) return repetido;
        if (await AcimaDoLimiteAsync(db, limites, tenant, ct) is { } limite) return limite;
        Loja loja;
        try
        {
            loja = Loja.Criar(empresa.EmpresaId!.Value, r.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        db.Lojas.Add(loja);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/estoque/lojas/{loja.Id}", new LojaDto(loja.Id, loja.EmpresaId, loja.Nome, true, 0));
    }

    private static async Task<IResult> Renomear(Guid id, LojaRequest r, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (ExigirDono(empresa) is { } negado) return negado;
        var daEmpresa = await lojas.VisiveisAsync(ct);
        var loja = daEmpresa.FirstOrDefault(l => l.Id == id);
        if (loja is null) return Results.NotFound();
        if (NomeRepetido(daEmpresa, r.Nome ?? "", id) is { } repetido) return repetido;
        try
        {
            loja.Renomear(r.Nome ?? "");
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok((await ComQuantidadesAsync(db, [loja], ct))[0]);
    }

    private static async Task<IResult> Desativar(Guid id, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (ExigirDono(empresa) is { } negado) return negado;
        var daEmpresa = await lojas.VisiveisAsync(ct);
        var loja = daEmpresa.FirstOrDefault(l => l.Id == id);
        if (loja is null) return Results.NotFound();
        if (!daEmpresa.Any(l => l.Ativa && l.Id != id))
            return Permissoes.Problema(StatusCodes.Status409Conflict, "A empresa precisa de ao menos uma loja ativa.");
        if (await db.Saldos.AnyAsync(s => s.LojaId == id && s.Quantidade != 0, ct))
            return Permissoes.Problema(StatusCodes.Status409Conflict, $"{loja.Nome} ainda tem estoque. Transfira ou ajuste as peças antes de desativar.");
        loja.Desativar();
        await db.SaveChangesAsync(ct);
        return Results.Ok((await ComQuantidadesAsync(db, [loja], ct))[0]);
    }

    private static async Task<IResult> Reativar(Guid id, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, ILimitesDoCliente limites,
        ITenantContext tenant, CancellationToken ct)
    {
        if (ExigirDono(empresa) is { } negado) return negado;
        var loja = (await lojas.VisiveisAsync(ct)).FirstOrDefault(l => l.Id == id);
        if (loja is null) return Results.NotFound();
        if (!loja.Ativa)
        {
            if (await AcimaDoLimiteAsync(db, limites, tenant, ct) is { } limite) return limite;
            loja.Reativar();
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok((await ComQuantidadesAsync(db, [loja], ct))[0]);
    }
}
