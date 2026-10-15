namespace Kamus.SharedKernel.Financeiro;

public sealed record ParcelaAPagar(DateOnly Vencimento, decimal Valor);

/// <param name="Origem">Quem gerou (ex.: "NotaDeEntrada"). Com <paramref name="OrigemId"/> (ex.: a chave da NF-e), registrar de novo não duplica.</param>
public sealed record ContaAPagarDeCompra(Guid EmpresaId, string Origem, string OrigemId, string Descricao, Guid? FornecedorId, string FornecedorNome,
    string? Documento, DateOnly Competencia, IReadOnlyList<ParcelaAPagar> Parcelas);

/// <summary>Contas a pagar geradas por outros módulos (ex.: duplicatas da NF-e lançada no estoque). Implementado pelo módulo Financeiro.</summary>
public interface IContasAPagar
{
    /// <returns>Id do título (o já existente, se a origem já foi registrada).</returns>
    Task<Guid> RegistrarCompraAsync(ContaAPagarDeCompra conta, CancellationToken ct = default);
}
