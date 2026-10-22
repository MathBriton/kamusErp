namespace Kamus.SharedKernel.Indicadores;

/// <summary>Códigos das etapas dos "Primeiros passos" do dashboard.</summary>
public static class EtapasDaImplantacao
{
    public const string Produtos = "produtos";
    public const string Clientes = "clientes";
    public const string EntradaDeEstoque = "entrada_de_estoque";
    public const string ContasAPagar = "contas_a_pagar";
    public const string FormasDePagamento = "formas_de_pagamento";
    public const string PrimeiraVenda = "primeira_venda";
}

/// <summary>Cada módulo informa quais etapas da implantação já foram feitas nas empresas (o dashboard junta todos).</summary>
public interface IProgressoDaImplantacao
{
    Task<IReadOnlyCollection<string>> EtapasConcluidasAsync(IReadOnlyCollection<Guid> empresaIds, CancellationToken ct = default);
}
