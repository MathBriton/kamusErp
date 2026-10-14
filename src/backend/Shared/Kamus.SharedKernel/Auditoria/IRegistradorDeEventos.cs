namespace Kamus.SharedKernel.Auditoria;

/// <summary>
/// Registra no histórico eventos que não são alteração de dados (login, logout, exportação...).
/// </summary>
public interface IRegistradorDeEventos
{
    /// <param name="evento">Código estável (ex.: <c>login</c>, <c>login_falhou</c>).</param>
    /// <param name="descricao">Texto pronto para exibir.</param>
    /// <param name="usuarioId">Informe quando o usuário ainda não está no token (ex.: no próprio login).</param>
    Task RegistrarAsync(string evento, string descricao, Guid? usuarioId = null, string? usuarioNome = null, CancellationToken ct = default);
}
