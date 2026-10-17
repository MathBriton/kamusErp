namespace Kamus.SharedKernel.Estoque;

public sealed record ItemDeSaida(Guid SkuId, int Quantidade);

/// <param name="PermitirNegativo">Venda pode deixar o saldo negativo (configuração da empresa); a resposta avisa quais ficaram.</param>
public sealed record SaidaDeEstoque(Guid EmpresaId, Guid LojaId, string Documento, IReadOnlyList<ItemDeSaida> Itens, bool PermitirNegativo);

/// <param name="CustoUnitario">Custo médio no momento da saída (base do lucro bruto da venda).</param>
public sealed record ItemSaido(Guid SkuId, int Quantidade, decimal CustoUnitario, int SaldoApos);

public sealed record SaidaRegistrada(Guid OperacaoId, IReadOnlyList<ItemSaido> Itens);

/// <summary>Saída recusada (loja inválida ou saldo insuficiente quando a empresa não permite negativo). A mensagem vai para a tela.</summary>
public sealed class SaidaDeEstoqueRecusadaException(string mensagem) : Exception(mensagem);

/// <summary>Baixa e devolução de estoque por outros módulos (vendas). Implementado pelo módulo Estoque.</summary>
public interface IMovimentacaoDeEstoque
{
    Task<SaidaRegistrada> RegistrarVendaAsync(SaidaDeEstoque saida, CancellationToken ct = default);

    /// <summary>Devolve ao estoque o que a operação tirou (cancelamento de venda). Chamar de novo não devolve duas vezes.</summary>
    Task EstornarVendaAsync(Guid operacaoId, string documento, CancellationToken ct = default);
}
