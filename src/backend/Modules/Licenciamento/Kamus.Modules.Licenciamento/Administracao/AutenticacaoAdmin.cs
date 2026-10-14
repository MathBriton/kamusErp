using System.Security.Claims;
using System.Text;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kamus.Modules.Licenciamento.Administracao;

internal sealed class AutenticacaoAdmin(
    LicenciamentoDbContext db, IPasswordHasher<Administrador> hasher, IOptions<AdminOptions> opcoes, TimeProvider relogio)
{
    public const string ClaimPapel = "papel";
    public const string PapelSuperAdmin = "superadmin";
    private const string Invalido = "E-mail ou senha inválidos.";

    public sealed record Sessao(string Token, DateTimeOffset ExpiraEm, string Nome, string Email);

    public static SymmetricSecurityKey Chave(AdminOptions o) => new(Encoding.UTF8.GetBytes(o.Chave));

    /// <returns>A sessão, ou o motivo da recusa (pronto para exibir).</returns>
    public async Task<(Sessao? Sessao, string? Erro)> EntrarAsync(string? email, string? senha, string? ip, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var normalizado = Administrador.NormalizarEmail(email);
        var admin = normalizado is null ? null : await db.Administradores.SingleOrDefaultAsync(a => a.Email == normalizado, ct);
        if (admin is null || !admin.Ativo || string.IsNullOrEmpty(senha))
            return (null, Invalido);

        if (admin.EstaBloqueado(agora))
            return (null, "Acesso bloqueado por excesso de tentativas. Aguarde 15 minutos.");

        if (hasher.VerifyHashedPassword(admin, admin.SenhaHash, senha) == PasswordVerificationResult.Failed)
        {
            admin.RegistrarFalhaDeLogin(agora);
            db.AcoesAdministrativas.Add(AcaoAdministrativa.Criar(admin.Id, admin.Email, "login_falhou", null, "Senha incorreta", ip, agora));
            await db.SaveChangesAsync(ct);
            return (null, admin.EstaBloqueado(agora) ? "Acesso bloqueado por excesso de tentativas. Aguarde 15 minutos." : Invalido);
        }

        admin.RegistrarLoginComSucesso(agora);
        db.AcoesAdministrativas.Add(AcaoAdministrativa.Criar(admin.Id, admin.Email, "login", null, "Entrou no Painel Administrativo", ip, agora));
        await db.SaveChangesAsync(ct);
        return (CriarSessao(admin, agora), null);
    }

    private Sessao CriarSessao(Administrador admin, DateTimeOffset agora)
    {
        var o = opcoes.Value;
        var expiraEm = agora.AddHours(o.HorasDoToken);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Emissor,
            Audience = o.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(Chave(o), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, admin.Email),
                new Claim(JwtRegisteredClaimNames.Name, admin.Nome),
                new Claim(ClaimPapel, PapelSuperAdmin),
            ]),
        });
        return new Sessao(token, expiraEm, admin.Nome, admin.Email);
    }

    /// <summary>Cria (ou mantém) um SuperAdmin. Usado pelo seed de Development e pelo primeiro acesso em produção.</summary>
    public async Task GarantirAsync(string nome, string email, string senha, CancellationToken ct)
    {
        var normalizado = Administrador.NormalizarEmail(email) ?? throw new InvalidOperationException("E-mail do SuperAdmin inválido.");
        if (await db.Administradores.AnyAsync(a => a.Email == normalizado, ct)) return;

        var admin = Administrador.Criar(nome, normalizado, relogio.GetUtcNow());
        admin.DefinirSenhaHash(hasher.HashPassword(admin, senha));
        db.Administradores.Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
