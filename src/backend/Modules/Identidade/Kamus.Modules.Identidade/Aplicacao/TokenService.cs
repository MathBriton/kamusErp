using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kamus.Modules.Identidade.Aplicacao;

internal sealed class TokenService(IOptions<JwtOptions> options)
{
    /// <summary>Presente no token de quem está com senha provisória: só as rotas de autenticação respondem.</summary>
    public const string ClaimTrocarSenha = "trocar_senha";

    private readonly JwtOptions _opcoes = options.Value;

    public SymmetricSecurityKey ChaveDeAssinatura => CriarChave(_opcoes);

    public static SymmetricSecurityKey CriarChave(JwtOptions opcoes) => new(Encoding.UTF8.GetBytes(opcoes.Chave));

    public (string Token, DateTimeOffset ExpiraEm) CriarTokenDeAcesso(Usuario usuario, DateTimeOffset agora)
    {
        var expiraEm = agora.AddMinutes(_opcoes.MinutosDoTokenDeAcesso);
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(ChaveDeAssinatura, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(JwtRegisteredClaimNames.Name, usuario.Nome),
                new Claim(MultiTenancyExtensions.TenantClaim, usuario.TenantId.ToString()),
                .. usuario.DeveTrocarSenha ? [new Claim(ClaimTrocarSenha, "true")] : Array.Empty<Claim>(),
            ]),
        };
        return (new JsonWebTokenHandler().CreateToken(descritor), expiraEm);
    }

    /// <summary>Token opaco aleatório (vai no cookie) e seu hash SHA-256 (vai no banco).</summary>
    public static (string Token, string Hash) CriarTokenDeSessao()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public TimeSpan ValidadeDaSessao(bool manterConectado) =>
        manterConectado ? TimeSpan.FromDays(_opcoes.DiasDaSessaoLembrada) : TimeSpan.FromHours(_opcoes.HorasDaSessaoNaoLembrada);
}
