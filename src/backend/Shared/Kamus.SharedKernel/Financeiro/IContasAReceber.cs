namespace Kamus.SharedKernel.Financeiro;

public sealed record ParcelaAReceber(DateOnly Vencimento, decimal Valor);

/// <param name="JaRecebido">Dinheiro/PIX: entra na hora na conta <paramref name="ContaId"/> (título já baixado). Cartão/outros: fica a receber.</param>
public sealed record RecebimentoDeVenda(string Descricao, IReadOnlyList<ParcelaAReceber> Parcelas, Guid? ContaId, bool JaRecebido, string? Observacao);

/// <param name="OrigemId">Identifica a venda: registrar de novo não duplica, e o cancelamento usa a mesma origem.</param>
public sealed record ContasAReceberDeVenda(Guid EmpresaId, string Origem, string OrigemId, Guid? ClienteId, string? ClienteNome, DateOnly Data,
    IReadOnlyList<RecebimentoDeVenda> Recebimentos);

/// <summary>Contas a receber geradas por outros módulos (vendas). Implementado pelo módulo Financeiro.</summary>
public interface IContasAReceber
{
    /// <summary>A conta financeira existe, está ativa e é da empresa?</summary>
    Task<bool> ContaValidaAsync(Guid empresaId, Guid contaId, CancellationToken ct = default);

    Task RegistrarVendaAsync(ContasAReceberDeVenda venda, CancellationToken ct = default);

    /// <summary>Estorna as baixas (movimento contrário na conta) e cancela os títulos da origem.</summary>
    Task CancelarOrigemAsync(string origem, string origemId, string? usuario, CancellationToken ct = default);
}
