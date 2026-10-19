namespace Kamus.Modules.Identidade.Aplicacao;

public sealed record LoginRequest(string? Email, string? Senha, bool ManterConectado = false);

/// <param name="DeveTrocarSenha">Senha provisória: o sistema só libera depois de <c>POST /api/auth/trocar-senha</c>.</param>
public sealed record UsuarioDto(Guid Id, string Nome, string Email, bool DeveTrocarSenha);

public sealed record TrocarSenhaRequest(string? SenhaAtual, string? NovaSenha);

public sealed record EmpresaDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj, string Perfil);

public sealed record SessaoResponse(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario, IReadOnlyList<EmpresaDto> Empresas);

public sealed record MeResponse(UsuarioDto Usuario, IReadOnlyList<EmpresaDto> Empresas);

/// <summary>Sessão emitida: resposta para o cliente + token de renovação (vai no cookie).</summary>
/// <summary>Sessão do app mobile: o token de sessão (renovação) vai no corpo, para o app guardar no armazenamento seguro do aparelho.</summary>
public sealed record SessaoMobileResponse(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario, IReadOnlyList<EmpresaDto> Empresas, string TokenDeSessao,
    DateTimeOffset SessaoExpiraEm);

public sealed record TokenDeSessaoRequest(string? TokenDeSessao);

internal sealed record SessaoEmitida(SessaoResponse Resposta, string TokenDeSessao, DateTimeOffset SessaoExpiraEm, bool ManterConectado);

internal abstract record ResultadoLogin
{
    public sealed record Sucesso(SessaoEmitida Sessao) : ResultadoLogin;
    public sealed record CredenciaisInvalidas : ResultadoLogin;
    public sealed record Bloqueado(DateTimeOffset Ate) : ResultadoLogin;
}
