using System.Security.Claims;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Consultas;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kamus.Modules.Identidade.Endpoints;

/// <summary>Administrativo → Usuários e senhas (perfil Dono).</summary>
internal static class UsuariosEndpoints
{
    public sealed record AcessoDto(Guid EmpresaId, string Empresa, string Perfil);
    public sealed record UsuarioDoClienteDto(Guid Id, string Nome, string Email, bool Ativo, bool DeveTrocarSenha, DateTimeOffset? UltimoLoginEm, IReadOnlyList<AcessoDto> Acessos);
    public sealed record AcessoRequest(Guid EmpresaId, string? Perfil);
    /// <summary><c>Senha</c> opcional: sem ela, o sistema gera uma provisória. Em ambos os casos a pessoa troca no primeiro acesso.</summary>
    public sealed record NovoUsuarioRequest(string? Nome, string? Email, IReadOnlyList<AcessoRequest>? Acessos, string? Senha = null);
    public sealed record UsuarioRequest(string? Nome, IReadOnlyList<AcessoRequest>? Acessos);
    public sealed record SenhaProvisoriaResponse(Guid Id, string Email, string SenhaProvisoria);

    public static IEndpointRouteBuilder MapUsuarios(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/usuarios").RequireAuthorization().WithTags("Usuários");
        grupo.MapGet("", Listar).WithSummary(
            "Usuários do cliente com os perfis por empresa — perfil Dono. Filtros: busca (nome ou e-mail), situacao (ativos|inativos|todos), empresaId, perfil; " +
            "com pagina, devolve uma página").Produces<IReadOnlyList<UsuarioDoClienteDto>>();
        grupo.MapPost("/situacao", MudarEmLote).WithSummary("Desativar ou reativar vários usuários (mesmas regras da unitária, tudo ou nada)")
            .Produces<SituacaoEmLoteResponse>();
        grupo.MapPost("", Criar).WithSummary("Cadastrar usuário com senha provisória (troca obrigatória no primeiro acesso)")
            .Produces<SenhaProvisoriaResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        grupo.MapPut("/{id:guid}", Editar).WithSummary("Alterar nome e perfis por empresa").Produces(StatusCodes.Status204NoContent);
        grupo.MapPost("/{id:guid}/desativar", Desativar).WithSummary("Desativar (não entra mais; sessões encerradas)").Produces(StatusCodes.Status204NoContent);
        grupo.MapPost("/{id:guid}/reativar", Reativar).WithSummary("Reativar (respeita o limite do plano)").Produces(StatusCodes.Status204NoContent);
        grupo.MapPost("/{id:guid}/redefinir-senha", RedefinirSenha).WithSummary("Gerar nova senha provisória (sessões encerradas)")
            .Produces<SenhaProvisoriaResponse>();
        return app;
    }

    private static Guid UsuarioLogado(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static IResult NaoEncontrado() => Gestao.Problema(StatusCodes.Status404NotFound, "Não encontrado", "Usuário não encontrado.");

    private static IResult Invalido(string detalhe) => Gestao.Problema(StatusCodes.Status400BadRequest, "Dados inválidos", detalhe);

    /// <summary>Usuários do tenant atual (a tabela de usuários não tem RLS: o filtro é explícito).</summary>
    private static IQueryable<Usuario> DoCliente(IdentidadeDbContext db, ITenantContext tenant) =>
        db.Usuarios.Where(u => u.TenantId == tenant.TenantId);

    /// <summary>Usuários do cliente com os filtros da tela (ativos primeiro, por nome).</summary>
    internal static async Task<List<UsuarioDoClienteDto>> FiltrarAsync(IdentidadeDbContext db, ITenantContext tenant, string? busca, string? situacao,
        Guid? empresaId, string? perfil, CancellationToken ct)
    {
        var usuarios = await DoCliente(db, tenant).AsNoTracking().ToListAsync(ct);
        var acessos = await (
            from a in db.AcessosEmpresa.AsNoTracking()
            join e in db.Empresas.AsNoTracking() on a.EmpresaId equals e.Id
            select new { a.UsuarioId, a.EmpresaId, e.NomeFantasia, a.Perfil }).ToListAsync(ct);

        IEnumerable<UsuarioDoClienteDto> lista = usuarios
            .OrderByDescending(u => u.Ativo).ThenBy(u => u.Nome, StringComparer.CurrentCulture)
            .Select(u => new UsuarioDoClienteDto(u.Id, u.Nome, u.Email, u.Ativo, u.DeveTrocarSenha, u.UltimoLoginEm,
                acessos.Where(a => a.UsuarioId == u.Id)
                    .OrderBy(a => a.NomeFantasia, StringComparer.CurrentCulture)
                    .Select(a => new AcessoDto(a.EmpresaId, a.NomeFantasia, a.Perfil.ToString())).ToList()));
        if (!string.IsNullOrWhiteSpace(busca)) lista = lista.Where(u => TextoParaBusca.Contem(u.Nome, busca) || TextoParaBusca.Contem(u.Email, busca));
        lista = situacao?.ToLowerInvariant() switch
        {
            "ativos" => lista.Where(u => u.Ativo),
            "inativos" => lista.Where(u => !u.Ativo),
            _ => lista,
        };
        if (empresaId is not null) lista = lista.Where(u => u.Acessos.Any(a => a.EmpresaId == empresaId));
        if (!string.IsNullOrWhiteSpace(perfil))
            lista = lista.Where(u => u.Acessos.Any(a => (empresaId is null || a.EmpresaId == empresaId) && string.Equals(a.Perfil, perfil, StringComparison.OrdinalIgnoreCase)));
        return [.. lista];
    }

    private static async Task<IResult> Listar(IEmpresaAtiva empresaAtiva, ITenantContext tenant, IdentidadeDbContext db, CancellationToken ct,
        string? busca = null, string? situacao = null, Guid? empresaId = null, string? perfil = null, int? pagina = null, int tamanho = Paginacao.TamanhoPadrao)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var usuarios = await FiltrarAsync(db, tenant, busca, situacao, empresaId, perfil, ct);
        return pagina is null ? Results.Ok(usuarios) : Results.Ok(usuarios.Paginar(pagina.Value, tamanho));
    }

    private static async Task<IResult> MudarEmLote(
        SituacaoEmLoteRequest r, IEmpresaAtiva empresaAtiva, ITenantContext tenant, ILimitesDoCliente limites, IdentidadeDbContext db,
        TimeProvider relogio, ClaimsPrincipal user, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var ids = (r.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) return Invalido("Selecione pelo menos um usuário.");
        var usuarios = await DoCliente(db, tenant).Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        if (usuarios.Count != ids.Count) return NaoEncontrado();
        var mudar = usuarios.Where(u => u.Ativo != r.Ativo).ToList();

        if (r.Ativo)
        {
            var limite = await LimiteAsync(limites, tenant, ct);
            if (limite is { } max && mudar.Count > 0 && await DoCliente(db, tenant).CountAsync(u => u.Ativo, ct) + mudar.Count > max)
                return Gestao.Limite("usuários ativos", max);
            foreach (var usuario in mudar) usuario.Reativar();
        }
        else
        {
            if (ids.Contains(UsuarioLogado(user))) return Invalido("Você não pode desativar o próprio usuário.");
            var restantes = DoCliente(db, tenant).Where(u => u.Ativo && !ids.Contains(u.Id)).Select(u => u.Id);
            if (!await db.AcessosEmpresa.AnyAsync(a => a.Perfil == Perfil.Dono && restantes.Contains(a.UsuarioId), ct))
                return Invalido("O cliente precisa de pelo menos um usuário ativo com perfil Dono.");
            foreach (var usuario in mudar)
            {
                usuario.Desativar();
                await RevogarSessoesAsync(usuario.Id, db, relogio, ct);
            }
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new SituacaoEmLoteResponse(mudar.Count));
    }

    /// <summary>Valida e converte os acessos pedidos (empresas do próprio cliente, perfis conhecidos, sem repetição).</summary>
    private static async Task<(List<(Guid EmpresaId, Perfil Perfil)>? Acessos, string? Erro)> LerAcessosAsync(
        IReadOnlyList<AcessoRequest>? pedidos, IdentidadeDbContext db, CancellationToken ct)
    {
        if (pedidos is null || pedidos.Count == 0) return (null, "Dê acesso a pelo menos uma empresa.");
        if (pedidos.Select(p => p.EmpresaId).Distinct().Count() != pedidos.Count) return (null, "Cada empresa pode aparecer uma vez só.");

        var ids = pedidos.Select(p => p.EmpresaId).ToList();
        if (await db.Empresas.CountAsync(e => ids.Contains(e.Id), ct) != ids.Count) return (null, "Empresa não encontrada.");

        var acessos = new List<(Guid, Perfil)>();
        foreach (var p in pedidos)
        {
            if (!Enum.TryParse<Perfil>(p.Perfil, ignoreCase: true, out var perfil) || !Enum.IsDefined(perfil) || int.TryParse(p.Perfil, out _))
                return (null, $"Perfil inválido: {p.Perfil}. Use Dono, Gerente, Vendedor, Estoquista ou Financeiro.");
            acessos.Add((p.EmpresaId, perfil));
        }
        return (acessos, null);
    }

    private static async Task<bool> LimiteAtingidoAsync(ILimitesDoCliente limites, ITenantContext tenant, IdentidadeDbContext db, CancellationToken ct)
    {
        var limite = (await limites.ObterAsync(tenant.TenantId!.Value, ct)).Usuarios;
        return limite is { } max && await DoCliente(db, tenant).CountAsync(u => u.Ativo, ct) >= max;
    }

    private static async Task<int?> LimiteAsync(ILimitesDoCliente limites, ITenantContext tenant, CancellationToken ct) =>
        (await limites.ObterAsync(tenant.TenantId!.Value, ct)).Usuarios;

    /// <summary>Verdadeiro se, sem considerar <paramref name="exceto"/>, ainda sobra algum Dono ativo.</summary>
    private static async Task<bool> HaOutroDonoAtivoAsync(Guid exceto, ITenantContext tenant, IdentidadeDbContext db, CancellationToken ct)
    {
        var ativos = DoCliente(db, tenant).Where(u => u.Ativo && u.Id != exceto).Select(u => u.Id);
        return await db.AcessosEmpresa.AnyAsync(a => a.Perfil == Perfil.Dono && ativos.Contains(a.UsuarioId), ct);
    }

    private static async Task<IResult> Criar(
        NovoUsuarioRequest r, IEmpresaAtiva empresaAtiva, ITenantContext tenant, ILimitesDoCliente limites,
        IdentidadeDbContext db, IPasswordHasher<Usuario> hasher, IRegistradorDeEventos eventos, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        if (string.IsNullOrWhiteSpace(r.Nome)) return Invalido("Informe o nome.");
        if (Usuario.NormalizarEmail(r.Email) is not { } email) return Invalido("E-mail inválido.");
        if (!string.IsNullOrEmpty(r.Senha) && PoliticaDeSenha.Validar(r.Senha) is { } senhaFraca) return Invalido(senhaFraca);
        var (acessos, erro) = await LerAcessosAsync(r.Acessos, db, ct);
        if (acessos is null) return Invalido(erro!);
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Gestao.Problema(StatusCodes.Status409Conflict, "E-mail em uso", "Já existe um usuário com este e-mail.");
        if (await LimiteAtingidoAsync(limites, tenant, db, ct)) return Gestao.Limite("usuários ativos", (await LimiteAsync(limites, tenant, ct))!.Value);

        var usuario = Usuario.Criar(tenant.TenantId!.Value, r.Nome, email);
        var senha = string.IsNullOrEmpty(r.Senha) ? PoliticaDeSenha.GerarProvisoria() : r.Senha;
        usuario.DefinirSenhaProvisoria(hasher.HashPassword(usuario, senha));
        db.Usuarios.Add(usuario);
        db.AcessosEmpresa.AddRange(acessos.Select(a => AcessoEmpresa.Criar(usuario.Id, a.EmpresaId, a.Perfil)));
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("usuario_criado", $"Usuário {usuario.Email} criado com senha {(string.IsNullOrEmpty(r.Senha) ? "provisória gerada" : "definida pelo Dono")}", ct: ct);
        return Results.Created($"/api/usuarios/{usuario.Id}", new SenhaProvisoriaResponse(usuario.Id, usuario.Email, senha));
    }

    private static async Task<IResult> Editar(
        Guid id, UsuarioRequest r, IEmpresaAtiva empresaAtiva, ITenantContext tenant, IdentidadeDbContext db, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var usuario = await DoCliente(db, tenant).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NaoEncontrado();
        if (string.IsNullOrWhiteSpace(r.Nome)) return Invalido("Informe o nome.");
        var (acessos, erro) = await LerAcessosAsync(r.Acessos, db, ct);
        if (acessos is null) return Invalido(erro!);
        if (!acessos.Any(a => a.Perfil == Perfil.Dono) && !await HaOutroDonoAtivoAsync(id, tenant, db, ct))
            return Invalido("O cliente precisa de pelo menos um usuário ativo com perfil Dono.");

        usuario.Renomear(r.Nome);
        var atuais = await db.AcessosEmpresa.Where(a => a.UsuarioId == id).ToListAsync(ct);
        foreach (var atual in atuais)
        {
            var pedido = acessos.FirstOrDefault(a => a.EmpresaId == atual.EmpresaId);
            if (pedido == default) db.AcessosEmpresa.Remove(atual);
            else if (pedido.Perfil != atual.Perfil) atual.AlterarPerfil(pedido.Perfil);
        }
        db.AcessosEmpresa.AddRange(acessos.Where(a => atuais.All(x => x.EmpresaId != a.EmpresaId))
            .Select(a => AcessoEmpresa.Criar(id, a.EmpresaId, a.Perfil)));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task RevogarSessoesAsync(Guid usuarioId, IdentidadeDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        foreach (var sessao in await db.RefreshTokens.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null).ToListAsync(ct))
            sessao.Revogar(agora);
    }

    private static async Task<IResult> Desativar(
        Guid id, IEmpresaAtiva empresaAtiva, ITenantContext tenant, IdentidadeDbContext db, TimeProvider relogio, ClaimsPrincipal user, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        if (id == UsuarioLogado(user)) return Invalido("Você não pode desativar o próprio usuário.");
        var usuario = await DoCliente(db, tenant).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NaoEncontrado();
        if (!await HaOutroDonoAtivoAsync(id, tenant, db, ct))
            return Invalido("O cliente precisa de pelo menos um usuário ativo com perfil Dono.");

        usuario.Desativar();
        await RevogarSessoesAsync(id, db, relogio, ct);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Reativar(
        Guid id, IEmpresaAtiva empresaAtiva, ITenantContext tenant, ILimitesDoCliente limites, IdentidadeDbContext db, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        var usuario = await DoCliente(db, tenant).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NaoEncontrado();
        if (usuario.Ativo) return Results.NoContent();
        if (await LimiteAtingidoAsync(limites, tenant, db, ct)) return Gestao.Limite("usuários ativos", (await LimiteAsync(limites, tenant, ct))!.Value);

        usuario.Reativar();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RedefinirSenha(
        Guid id, IEmpresaAtiva empresaAtiva, ITenantContext tenant, IdentidadeDbContext db, IPasswordHasher<Usuario> hasher,
        TimeProvider relogio, IRegistradorDeEventos eventos, ClaimsPrincipal user, CancellationToken ct)
    {
        if (Gestao.ExigirDono(empresaAtiva) is { } negado) return negado;
        if (id == UsuarioLogado(user)) return Invalido("Para a sua própria senha, use \"Trocar senha\" no menu do usuário.");
        var usuario = await DoCliente(db, tenant).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NaoEncontrado();

        var senha = PoliticaDeSenha.GerarProvisoria();
        usuario.DefinirSenhaProvisoria(hasher.HashPassword(usuario, senha));
        await RevogarSessoesAsync(id, db, relogio, ct);
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("senha_redefinida", $"Senha de {usuario.Email} redefinida (provisória)", ct: ct);
        return Results.Ok(new SenhaProvisoriaResponse(usuario.Id, usuario.Email, senha));
    }
}
