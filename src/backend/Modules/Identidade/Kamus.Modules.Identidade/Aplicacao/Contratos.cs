namespace Kamus.Modules.Identidade.Aplicacao;

public sealed record LoginRequest(string? Email, string? Senha, bool ManterConectado = false);

public sealed record UsuarioDto(Guid Id, string Nome, string Email);

public sealed record EmpresaDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj, string Perfil);

public sealed record SessaoResponse(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario, IReadOnlyList<EmpresaDto> Empresas);

public sealed record MeResponse(UsuarioDto Usuario, IReadOnlyList<EmpresaDto> Empresas);

/// <summary>Sessão emitida: resposta para o cliente + token de renovação (vai no cookie).</summary>
internal sealed record SessaoEmitida(SessaoResponse Resposta, string TokenDeSessao, DateTimeOffset SessaoExpiraEm, bool ManterConectado);

internal abstract record ResultadoLogin
{
    public sealed record Sucesso(SessaoEmitida Sessao) : ResultadoLogin;
    public sealed record CredenciaisInvalidas : ResultadoLogin;
    public sealed record Bloqueado(DateTimeOffset Ate) : ResultadoLogin;
}
