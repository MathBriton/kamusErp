using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Endpoints;

internal static class GradesECoresEndpoints
{
    public sealed record GradeRequest(string? Nome, IReadOnlyList<string>? Tamanhos);
    public sealed record GradeDto(Guid Id, string Nome, IReadOnlyList<string> Tamanhos, bool EmUso);
    public sealed record CorRequest(string? Nome, string? Hex);
    public sealed record CorDto(Guid Id, string Nome, string? Hex);

    public static IEndpointRouteBuilder MapGradesECores(this IEndpointRouteBuilder app)
    {
        var grades = app.MapGroup("/api/grades").RequireAuthorization().WithTags("Grades e cores");
        grades.MapGet("", ListarGrades).WithSummary("Grades de tamanho (em uso = já tem produto)").Produces<IReadOnlyList<GradeDto>>();
        grades.MapPost("", CriarGrade).WithSummary("Cadastrar grade (Dono, Gerente, Estoquista)").Produces<GradeDto>(StatusCodes.Status201Created);
        grades.MapPut("/{id:guid}", AlterarGrade).WithSummary("Alterar grade ainda sem produtos").Produces<GradeDto>();
        grades.MapPost("/padrao", GradesPadrao).WithSummary("Cria as grades padrão que ainda não existem").Produces(StatusCodes.Status204NoContent);

        var cores = app.MapGroup("/api/cores").RequireAuthorization().WithTags("Grades e cores");
        cores.MapGet("", ListarCores).WithSummary("Cores").Produces<IReadOnlyList<CorDto>>();
        cores.MapPost("", CriarCor).WithSummary("Cadastrar cor").Produces<CorDto>(StatusCodes.Status201Created);
        cores.MapPut("/{id:guid}", AlterarCor).WithSummary("Alterar cor").Produces<CorDto>();
        cores.MapPost("/padrao", CoresPadrao).WithSummary("Cria as cores padrão que ainda não existem").Produces(StatusCodes.Status204NoContent);
        return app;
    }

    internal static IResult Problema(int status, string detalhe) => Results.Problem(statusCode: status, title: "Não foi possível salvar", detail: detalhe);
    private static bool PodeEditar(IEmpresaAtiva e) => Permissoes.Catalogo.Contains(e.Perfil);
    private static readonly IResult NaoPode = Permissoes.Negado("Só Dono, Gerente e Estoquista alteram o catálogo.");

    private static async Task<IResult> ListarGrades(CadastrosDbContext db, CancellationToken ct)
    {
        var emUso = await db.Produtos.Select(p => p.GradeId).Distinct().ToListAsync(ct);
        var grades = await db.Grades.AsNoTracking().ToListAsync(ct);
        return Results.Ok(grades.OrderBy(g => g.Nome, StringComparer.CurrentCulture).Select(g => new GradeDto(g.Id, g.Nome, g.Tamanhos, emUso.Contains(g.Id))).ToList());
    }

    private static async Task<IResult> CriarGrade(GradeRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        GradeDeTamanhos grade;
        try
        {
            grade = GradeDeTamanhos.Criar(r.Nome ?? "", r.Tamanhos ?? []);
        }
        catch (ArgumentException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        if (await db.Grades.AnyAsync(g => g.Nome == grade.Nome, ct)) return Problema(StatusCodes.Status409Conflict, $"Já existe a grade \"{grade.Nome}\".");
        db.Grades.Add(grade);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/grades/{grade.Id}", new GradeDto(grade.Id, grade.Nome, grade.Tamanhos, false));
    }

    private static async Task<IResult> AlterarGrade(Guid id, GradeRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        var grade = await db.Grades.SingleOrDefaultAsync(g => g.Id == id, ct);
        if (grade is null) return Problema(StatusCodes.Status404NotFound, "Grade não encontrada.");
        if (await db.Produtos.AnyAsync(p => p.GradeId == id, ct))
            return Problema(StatusCodes.Status409Conflict, "Esta grade já tem produtos e não pode mudar. Crie uma grade nova.");
        try
        {
            grade.Alterar(r.Nome ?? "", r.Tamanhos ?? []);
        }
        catch (ArgumentException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new GradeDto(grade.Id, grade.Nome, grade.Tamanhos, false));
    }

    private static async Task<IResult> GradesPadrao(IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        var existentes = await db.Grades.Select(g => g.Nome).ToListAsync(ct);
        db.Grades.AddRange(GradeDeTamanhos.Padroes.Where(p => !existentes.Contains(p.Nome)).Select(p => GradeDeTamanhos.Criar(p.Nome, p.Tamanhos)));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListarCores(CadastrosDbContext db, CancellationToken ct) =>
        Results.Ok((await db.Cores.AsNoTracking().ToListAsync(ct)).OrderBy(c => c.Nome, StringComparer.CurrentCulture).Select(c => new CorDto(c.Id, c.Nome, c.Hex)).ToList());

    private static async Task<IResult> CriarCor(CorRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        Cor cor;
        try
        {
            cor = Cor.Criar(r.Nome ?? "", r.Hex);
        }
        catch (ArgumentException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        if (await db.Cores.AnyAsync(c => c.Nome == cor.Nome, ct)) return Problema(StatusCodes.Status409Conflict, $"Já existe a cor \"{cor.Nome}\".");
        db.Cores.Add(cor);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/cores/{cor.Id}", new CorDto(cor.Id, cor.Nome, cor.Hex));
    }

    private static async Task<IResult> AlterarCor(Guid id, CorRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        var cor = await db.Cores.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (cor is null) return Problema(StatusCodes.Status404NotFound, "Cor não encontrada.");
        try
        {
            cor.Alterar(r.Nome ?? "", r.Hex);
        }
        catch (ArgumentException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        if (await db.Cores.AnyAsync(c => c.Nome == cor.Nome && c.Id != id, ct)) return Problema(StatusCodes.Status409Conflict, $"Já existe a cor \"{cor.Nome}\".");
        await db.SaveChangesAsync(ct);
        return Results.Ok(new CorDto(cor.Id, cor.Nome, cor.Hex));
    }

    private static async Task<IResult> CoresPadrao(IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!PodeEditar(empresa)) return NaoPode;
        var existentes = await db.Cores.Select(c => c.Nome).ToListAsync(ct);
        db.Cores.AddRange(Cor.Padroes.Where(p => !existentes.Contains(p.Nome)).Select(p => Cor.Criar(p.Nome, p.Hex)));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
