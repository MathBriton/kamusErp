namespace Kamus.SharedKernel.Indicadores;

public sealed record SaldoDeConta(Guid Id, string Nome, string Tipo, decimal Saldo);

/// <param name="Hoje">Saldo das parcelas que vencem hoje.</param>
/// <param name="Amanha">Saldo das parcelas que vencem amanhã.</param>
/// <param name="ProximosSeteDias">De hoje até daqui a 6 dias.</param>
/// <param name="Vencido">Parcelas vencidas e não pagas (antes de hoje).</param>
public sealed record Compromissos(decimal Hoje, decimal Amanha, decimal ProximosSeteDias, decimal Vencido, int ParcelasVencidas);

public sealed record ResumoFinanceiro(IReadOnlyList<SaldoDeConta> Contas, decimal SaldoTotal, Compromissos AReceber, Compromissos APagar,
    decimal CrediarioEmAtraso, int ClientesEmAtraso);

/// <param name="Tipo">Receita ou Despesa.</param>
/// <param name="Origem">Quem gerou o título (Manual, Venda, Troca, NotaDeEntrada, FechamentoDeComissao...).</param>
public sealed record LancamentoPorCategoria(string Tipo, string Grupo, string Categoria, string Origem, decimal Valor);

/// <summary>Números financeiros para o dashboard e o resumo diário. Implementado pelo módulo Financeiro.</summary>
public interface IIndicadoresFinanceiros
{
    Task<ResumoFinanceiro> ResumoAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly hoje, CancellationToken ct = default);

    /// <summary>Títulos não cancelados pela competência no período, somados por tipo, categoria e origem (base da DRE).</summary>
    Task<IReadOnlyList<LancamentoPorCategoria>> PorCategoriaAsync(IReadOnlyCollection<Guid> empresaIds, DateOnly de, DateOnly ate, CancellationToken ct = default);
}
