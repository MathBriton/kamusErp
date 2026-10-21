using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Consultas;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Endpoints;

internal static class VendedoresEndpoints
{
    public sealed record VendedorRequest(string? Nome, decimal ComissaoPercentual, Guid? UsuarioId, string? Telefone);
    public sealed record VendedorDto(Guid Id, string Nome, decimal ComissaoPercentual, Guid? UsuarioId, string? UsuarioNome, string? Telefone, bool Ativo);
    public sealed record UsuarioDto(Guid Id, string Nome, string Email);

    public static IEndpointRouteBuilder MapVendedores(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendedores").RequireAuthorization().WithTags("Vendedores");
        g.MapGet("", Listar)
            .WithSummary("Vendedores (todos os perfis veem; só Dono/Gerente alteram). Sem pagina: lista inteira; com pagina/tamanho: { itens, total, pagina, tamanho }")
            .Produces<IReadOnlyList<VendedorDto>>().Produces<PaginaDeRegistros<VendedorDto>>();
        g.MapGet("/usuarios", Usuarios).WithSummary("Usuários ativos que podem ser vinculados a um vendedor (Dono/Gerente)").Produces<IReadOnlyList<UsuarioDto>>();
        g.MapPost("", Criar).WithSummary("Cadastrar vendedor").Produces<VendedorDto>(StatusCodes.Status201Created);
        g.MapPut("/{id:guid}", Editar).WithSummary("Alterar vendedor").Produces<VendedorDto>();
        g.MapPost("/{id:guid}/inativar", (Guid id, IEmpresaAtiva e, CadastrosDbContext db, CancellationToken ct) => Mudar(id, e, db, v => v.Inativar(), ct))
            .WithSummary("Inativar vendedor").Produces(StatusCodes.Status204NoContent);
        g.MapPost("/{id:guid}/reativar", (Guid id, IEmpresaAtiva e, CadastrosDbContext db, CancellationToken ct) => Mudar(id, e, db, v => v.Reativar(), ct))
            .WithSummary("Reativar vendedor").Produces(StatusCodes.Status204NoContent);
        g.MapPost("/situacao", MudarEmLote).WithSummary("Inativar ou reativar vários vendedores de uma vez (seleção da grade)").Produces<SituacaoEmLoteResponse>();
        return app;
    }

    private static IResult Invalido(string detalhe) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados inválidos", detail: detalhe);
    private static IResult NaoEncontrado() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Não encontrado", detail: "Vendedor não encontrado.");

    /// <summary>Vendedores filtrados por busca (nome ou telefone) e situação (ativos|inativos|todos; sem situação = todos), ativos primeiro.</summary>
    internal static async Task<List<VendedorDto>> FiltrarAsync(CadastrosDbContext db, IDiretorioDeClientes diretorio, string? busca, string? situacao, CancellationToken ct)
    {
        var usuarios = (await diretorio.UsuariosDoTenantAtualAsync(ct)).ToDictionary(u => u.Id, u => u.Nome);
        var consulta = db.Vendedores.AsNoTracking();
        consulta = situacao?.ToLowerInvariant() switch
        {
            "ativos" => consulta.Where(v => v.Ativo),
            "inativos" => consulta.Where(v => !v.Ativo),
            _ => consulta,
        };
        IEnumerable<Vendedor> vendedores = await consulta.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(busca))
            vendedores = vendedores.Where(v => TextoParaBusca.Contem(v.Nome, busca) || TextoParaBusca.ContemDigitos(v.Telefone, busca));
        return vendedores
            .OrderByDescending(v => v.Ativo).ThenBy(v => v.Nome, StringComparer.CurrentCulture)
            .Select(v => new VendedorDto(v.Id, v.Nome, v.ComissaoPercentual, v.UsuarioId,
                v.UsuarioId is { } u && usuarios.TryGetValue(u, out var nome) ? nome : null, v.Telefone, v.Ativo))
            .ToList();
    }

    /// <summary>Sem <c>pagina</c> devolve a lista inteira (seletores de venda, condicional e comissões); com ela, a página da grade.</summary>
    private static async Task<IResult> Listar(CadastrosDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct,
        string? busca = null, string? situacao = null, int? pagina = null, int tamanho = Paginacao.TamanhoPadrao)
    {
        var vendedores = await FiltrarAsync(db, diretorio, busca, situacao, ct);
        return pagina is { } p ? Results.Ok(vendedores.Paginar(p, tamanho)) : Results.Ok(vendedores);
    }

    private static async Task<IResult> Usuarios(IEmpresaAtiva empresa, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (!Permissoes.Gestao.Contains(empresa.Perfil)) return Permissoes.Negado();
        return Results.Ok((await diretorio.UsuariosDoTenantAtualAsync(ct)).Where(u => u.Ativo).Select(u => new UsuarioDto(u.Id, u.Nome, u.Email)).ToList());
    }

    private static async Task<IResult?> ValidarAsync(VendedorRequest r, Guid? exceto, CadastrosDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Nome)) return Invalido("Informe o nome.");
        if (r.ComissaoPercentual is < 0 or > 100) return Invalido("A comissão deve ficar entre 0% e 100%.");
        if (!string.IsNullOrWhiteSpace(r.Telefone) && Telefone.Normalizar(r.Telefone) is null) return Invalido("Telefone inválido: informe com DDD.");
        if (r.UsuarioId is not { } usuarioId) return null;
        if ((await diretorio.UsuariosDoTenantAtualAsync(ct)).All(u => u.Id != usuarioId)) return Invalido("Usuário não encontrado.");
        if (await db.Vendedores.AnyAsync(v => v.UsuarioId == usuarioId && v.Id != exceto, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Já vinculado", detail: "Este usuário já está vinculado a outro vendedor.");
        return null;
    }

    private static async Task<IResult> Criar(VendedorRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (!Permissoes.Gestao.Contains(empresa.Perfil)) return Permissoes.Negado();
        if (await ValidarAsync(r, null, db, diretorio, ct) is { } erro) return erro;

        var vendedor = Vendedor.Criar(r.Nome!, r.ComissaoPercentual, r.UsuarioId, r.Telefone);
        db.Vendedores.Add(vendedor);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/vendedores/{vendedor.Id}", new VendedorDto(vendedor.Id, vendedor.Nome, vendedor.ComissaoPercentual, vendedor.UsuarioId, null, vendedor.Telefone, vendedor.Ativo));
    }

    private static async Task<IResult> Editar(Guid id, VendedorRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (!Permissoes.Gestao.Contains(empresa.Perfil)) return Permissoes.Negado();
        var vendedor = await db.Vendedores.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (vendedor is null) return NaoEncontrado();
        if (await ValidarAsync(r, id, db, diretorio, ct) is { } erro) return erro;

        vendedor.Atualizar(r.Nome!, r.ComissaoPercentual, r.UsuarioId, r.Telefone);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new VendedorDto(vendedor.Id, vendedor.Nome, vendedor.ComissaoPercentual, vendedor.UsuarioId, null, vendedor.Telefone, vendedor.Ativo));
    }

    private static async Task<IResult> Mudar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, Action<Vendedor> acao, CancellationToken ct)
    {
        if (!Permissoes.Gestao.Contains(empresa.Perfil)) return Permissoes.Negado();
        var vendedor = await db.Vendedores.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (vendedor is null) return NaoEncontrado();
        acao(vendedor);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MudarEmLote(SituacaoEmLoteRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!Permissoes.Gestao.Contains(empresa.Perfil)) return Permissoes.Negado();
        var ids = (r.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) return Invalido("Selecione pelo menos um vendedor.");
        var vendedores = await db.Vendedores.Where(v => ids.Contains(v.Id) && v.Ativo != r.Ativo).ToListAsync(ct);
        foreach (var vendedor in vendedores)
        {
            if (r.Ativo) vendedor.Reativar();
            else vendedor.Inativar();
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new SituacaoEmLoteResponse(vendedores.Count));
    }
}
