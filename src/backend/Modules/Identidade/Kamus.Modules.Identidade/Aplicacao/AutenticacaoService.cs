using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Identidade.Aplicacao;

internal sealed class AutenticacaoService(
    IdentidadeDbContext db,
    IPasswordHasher<Usuario> hasher,
    TokenService tokens,
    TenantContext tenant,
    TimeProvider relogio)
{
    // Hash fixo usado quando o e-mail não existe: o tempo de resposta fica igual ao de uma senha errada.
    private static readonly Lazy<string> HashFicticio = new(() =>
        new PasswordHasher<Usuario>().HashPassword(Usuario.Criar(Guid.Empty, "x", "x@x.x"), "senha-ficticia"));

    public async Task<ResultadoLogin> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var email = Usuario.NormalizarEmail(request.Email);
        var usuario = email is null ? null : await db.Usuarios.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || !usuario.Ativo)
        {
            hasher.VerifyHashedPassword(null!, HashFicticio.Value, request.Senha ?? "");
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        if (usuario.EstaBloqueado(agora))
            return new ResultadoLogin.Bloqueado(usuario.BloqueadoAte!.Value);

        var verificacao = hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, request.Senha ?? "");
        if (verificacao == PasswordVerificationResult.Failed)
        {
            usuario.RegistrarFalhaDeLogin(agora);
            await db.SaveChangesAsync(ct);
            return usuario.EstaBloqueado(agora)
                ? new ResultadoLogin.Bloqueado(usuario.BloqueadoAte!.Value)
                : new ResultadoLogin.CredenciaisInvalidas();
        }

        if (verificacao == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.DefinirSenhaHash(hasher.HashPassword(usuario, request.Senha!));

        usuario.RegistrarLoginComSucesso(agora);
        return new ResultadoLogin.Sucesso(await EmitirSessaoAsync(usuario, request.ManterConectado, agora, substituindo: null, ct));
    }

    /// <summary>Troca o token de sessão por um novo (rotação). Reuso de token já trocado revoga todas as sessões.</summary>
    public async Task<SessaoEmitida?> RenovarAsync(string? tokenDeSessao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenDeSessao)) return null;

        var agora = relogio.GetUtcNow();
        var hash = TokenService.Hash(tokenDeSessao);
        var atual = await db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (atual is null) return null;

        if (atual.Revogado)
        {
            await RevogarTodasAsync(atual.UsuarioId, agora, ct);
            return null;
        }

        if (atual.Expirado(agora)) return null;

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == atual.UsuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.EstaBloqueado(agora)) return null;

        return await EmitirSessaoAsync(usuario, atual.ManterConectado, agora, substituindo: atual, ct);
    }

    public async Task SairAsync(string? tokenDeSessao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenDeSessao)) return;

        var hash = TokenService.Hash(tokenDeSessao);
        var atual = await db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (atual is null) return;

        atual.Revogar(relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task<MeResponse?> ObterUsuarioAtualAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
        return usuario is null ? null : new MeResponse(ParaDto(usuario), await EmpresasDoUsuarioAsync(usuario.Id, ct));
    }

    private async Task<SessaoEmitida> EmitirSessaoAsync(Usuario usuario, bool manterConectado, DateTimeOffset agora, RefreshToken? substituindo, CancellationToken ct)
    {
        // A partir daqui as consultas a dados do tenant (empresas, acessos) passam pelo filtro/RLS dele.
        tenant.Definir(usuario.TenantId);

        var (tokenDeSessao, hash) = TokenService.CriarTokenDeSessao();
        var novo = RefreshToken.Criar(usuario.Id, hash, manterConectado, agora, tokens.ValidadeDaSessao(manterConectado));
        db.RefreshTokens.Add(novo);
        substituindo?.Revogar(agora, novo.Id);
        await db.SaveChangesAsync(ct);

        var (accessToken, expiraEm) = tokens.CriarTokenDeAcesso(usuario, agora);
        var resposta = new SessaoResponse(accessToken, expiraEm, ParaDto(usuario), await EmpresasDoUsuarioAsync(usuario.Id, ct));
        return new SessaoEmitida(resposta, tokenDeSessao, novo.ExpiraEm, manterConectado);
    }

    private async Task<IReadOnlyList<EmpresaDto>> EmpresasDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var acessos = await (
            from a in db.AcessosEmpresa.AsNoTracking()
            join e in db.Empresas.AsNoTracking() on a.EmpresaId equals e.Id
            where a.UsuarioId == usuarioId
            select new { e.Id, e.NomeFantasia, e.RazaoSocial, e.Cnpj, a.Perfil }).ToListAsync(ct);

        return acessos
            .OrderBy(a => a.NomeFantasia, StringComparer.CurrentCulture)
            .Select(a => new EmpresaDto(a.Id, a.NomeFantasia, a.RazaoSocial, a.Cnpj.Formatado, a.Perfil.ToString()))
            .ToList();
    }

    private async Task RevogarTodasAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken ct)
    {
        var ativas = await db.RefreshTokens.Where(r => r.UsuarioId == usuarioId && r.RevogadoEm == null).ToListAsync(ct);
        foreach (var sessao in ativas) sessao.Revogar(agora);
        await db.SaveChangesAsync(ct);
    }

    private static UsuarioDto ParaDto(Usuario u) => new(u.Id, u.Nome, u.Email);
}
