using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Endpoints;

/// <summary>Plano de contas simples (grupo › categoria) do cliente, compartilhado entre as empresas.</summary>
internal static class CategoriasEndpoints
{
    public sealed record CategoriaRequest(string? Nome, string? Grupo, string? Tipo);
    public sealed record CategoriaDto(Guid Id, string Nome, string Grupo, string Tipo, bool Ativa);

    public static IEndpointRouteBuilder MapCategorias(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/financeiro/categorias").RequireAuthorization().WithTags("Financeiro");
        g.MapGet("/", Listar).WithSummary("Categorias (receitas e despesas) por grupo");
        g.MapPost("/padrao", Padrao).WithSummary("Cria as categorias padrão que ainda não existem");
        g.MapPost("/", Criar).WithSummary("Nova categoria").Produces<CategoriaDto>(StatusCodes.Status201Created);
        g.MapPut("/{id:guid}", Atualizar).WithSummary("Altera nome, grupo e tipo");
        g.MapPost("/{id:guid}/desativar", (Guid id, FinanceiroDbContext db, IEmpresaAtiva e, CancellationToken ct) => Alternar(id, false, db, e, ct)).WithSummary("Desativa");
        g.MapPost("/{id:guid}/reativar", (Guid id, FinanceiroDbContext db, IEmpresaAtiva e, CancellationToken ct) => Alternar(id, true, db, e, ct)).WithSummary("Reativa");
        return app;
    }

    private static CategoriaDto Dto(Categoria c) => new(c.Id, c.Nome, c.Grupo, c.Tipo.ToString(), c.Ativa);

    private static async Task<IResult> Listar(FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var categorias = await db.Categorias.AsNoTracking().ToListAsync(ct);
        return Results.Ok(categorias.OrderBy(c => c.Tipo).ThenBy(c => c.Grupo, StringComparer.CurrentCulture).ThenBy(c => c.Nome, StringComparer.CurrentCulture).Select(Dto).ToList());
    }

    private static async Task<IResult> Padrao(FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var existentes = (await db.Categorias.Select(c => new { c.Tipo, c.Nome }).ToListAsync(ct)).Select(c => (c.Tipo, TextoParaBusca.Normalizar(c.Nome))).ToHashSet();
        foreach (var (grupo, nome, tipo) in Categoria.Padroes.Where(p => !existentes.Contains((p.Tipo, TextoParaBusca.Normalizar(p.Nome)))))
            db.Categorias.Add(Categoria.Criar(nome, grupo, tipo));
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }

    private static bool TentarTipo(string? texto, out TipoDeCategoria tipo) => Enum.TryParse(texto, ignoreCase: true, out tipo) && Enum.IsDefined(tipo);

    private static async Task<bool> RepetidaAsync(FinanceiroDbContext db, TipoDeCategoria tipo, string? nome, Guid? exceto, CancellationToken ct)
    {
        var chave = TextoParaBusca.Normalizar(nome);
        return (await db.Categorias.Where(c => c.Tipo == tipo && c.Id != exceto).Select(c => c.Nome).ToListAsync(ct)).Any(n => TextoParaBusca.Normalizar(n) == chave);
    }

    private static async Task<IResult> Criar(CategoriaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo inválido: Receita ou Despesa.");
        if (await RepetidaAsync(db, tipo, r.Nome, null, ct)) return Permissoes.Problema(StatusCodes.Status409Conflict, $"Já existe a categoria {r.Nome?.Trim()}.");
        try
        {
            var categoria = Categoria.Criar(r.Nome ?? "", r.Grupo ?? "", tipo);
            db.Categorias.Add(categoria);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/financeiro/categorias/{categoria.Id}", Dto(categoria));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
    }

    private static async Task<IResult> Atualizar(Guid id, CategoriaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var categoria = await db.Categorias.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (categoria is null) return Results.NotFound();
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo inválido: Receita ou Despesa.");
        if (await RepetidaAsync(db, tipo, r.Nome, id, ct)) return Permissoes.Problema(StatusCodes.Status409Conflict, $"Já existe a categoria {r.Nome?.Trim()}.");
        try
        {
            categoria.Atualizar(r.Nome ?? "", r.Grupo ?? "", tipo);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(categoria));
    }

    private static async Task<IResult> Alternar(Guid id, bool ativa, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var categoria = await db.Categorias.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (categoria is null) return Results.NotFound();
        if (ativa) categoria.Reativar();
        else categoria.Desativar();
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(categoria));
    }
}
