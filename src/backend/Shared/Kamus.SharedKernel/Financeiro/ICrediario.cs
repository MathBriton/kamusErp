namespace Kamus.SharedKernel.Financeiro;

/// <param name="Disponivel">Limite menos o que está em aberto (nunca negativo).</param>
/// <param name="Motivo">Por que está bloqueado (texto pronto para a tela); nulo se liberado.</param>
public sealed record SituacaoDoCredito(decimal Limite, decimal EmAberto, decimal Disponivel, int DiasDeAtraso, bool Bloqueado, string? Motivo);

/// <summary>Crediário próprio (fiado): limite e situação do cliente para vender a prazo. Implementado pelo módulo Financeiro.</summary>
public interface ICrediario
{
    Task<SituacaoDoCredito> SituacaoAsync(Guid clienteId, CancellationToken ct = default);
}
