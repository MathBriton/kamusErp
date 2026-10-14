namespace Kamus.SharedKernel.Notificacoes;

/// <param name="Tipo">Código estável (ex.: <c>usuario_bloqueado</c>, <c>estoque_baixo</c>).</param>
/// <param name="Link">Rota do frontend aberta ao clicar (ex.: <c>/estoque/posicao</c>).</param>
public sealed record NovaNotificacao(Guid UsuarioId, string Tipo, string Titulo, string Mensagem, string? Link = null, Guid? EmpresaId = null);

/// <summary>Qualquer módulo notifica usuários por aqui: grava e entrega em tempo real (sininho).</summary>
public interface INotificador
{
    Task NotificarAsync(NovaNotificacao notificacao, CancellationToken ct = default);
}
