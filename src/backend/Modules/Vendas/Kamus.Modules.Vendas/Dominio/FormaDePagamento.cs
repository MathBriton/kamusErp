using System.ComponentModel;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

public enum TipoDePagamento
{
    Dinheiro,
    Pix,
    Debito,
    Credito,
    Outro,
    ValeTroca,
    Crediario,
}

/// <summary>
/// Como o cliente paga, com a taxa da maquininha/banco e o prazo para o dinheiro cair. Dinheiro e PIX entram na hora na conta
/// configurada; débito, crédito e outros viram contas a receber (já descontada a taxa).
/// </summary>
[DisplayName("Forma de pagamento")]
public sealed class FormaDePagamento : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = "";
    public TipoDePagamento Tipo { get; private set; }

    /// <summary>Conta financeira onde o dinheiro entra (obrigatória para dinheiro e PIX).</summary>
    public Guid? ContaId { get; private set; }

    public decimal TaxaAVista { get; private set; }
    public decimal TaxaParcelado { get; private set; }
    public int MaxParcelas { get; private set; } = 1;

    /// <summary>Dias até o dinheiro cair (crédito: da 1ª parcela; as demais a cada 30 dias).</summary>
    public int PrazoDias { get; private set; }

    public bool Ativa { get; private set; }

    public static readonly IReadOnlyList<(string Nome, TipoDePagamento Tipo, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias)> Padroes =
    [
        ("Dinheiro", TipoDePagamento.Dinheiro, 0m, 0m, 1, 0),
        ("PIX", TipoDePagamento.Pix, 0m, 0m, 1, 0),
        ("Cartão de débito", TipoDePagamento.Debito, 1.5m, 1.5m, 1, 1),
        ("Cartão de crédito", TipoDePagamento.Credito, 3.5m, 5.5m, 12, 30),
        ("Vale-troca", TipoDePagamento.ValeTroca, 0m, 0m, 1, 0),
        ("Crediário", TipoDePagamento.Crediario, 0m, 0m, 10, 30),
    ];

    public bool EntraNaHora => Tipo is TipoDePagamento.Dinheiro or TipoDePagamento.Pix;

    private FormaDePagamento() { }

    public static FormaDePagamento Criar(Guid empresaId, string nome, TipoDePagamento tipo, Guid? contaId, decimal taxaAVista, decimal taxaParcelado, int maxParcelas, int prazoDias)
    {
        var forma = new FormaDePagamento { Id = Guid.NewGuid(), EmpresaId = empresaId, Ativa = true };
        forma.Atualizar(nome, tipo, contaId, taxaAVista, taxaParcelado, maxParcelas, prazoDias);
        return forma;
    }

    public void Atualizar(string nome, TipoDePagamento tipo, Guid? contaId, decimal taxaAVista, decimal taxaParcelado, int maxParcelas, int prazoDias)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome da forma de pagamento.", nameof(nome));
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("Tipo inválido.", nameof(tipo));
        if (taxaAVista is < 0 or > 100 || taxaParcelado is < 0 or > 100) throw new ArgumentException("A taxa vai de 0% a 100%.", nameof(taxaAVista));
        if (maxParcelas is < 1 or > 24) throw new ArgumentException("Parcelas: de 1 a 24.", nameof(maxParcelas));
        if (prazoDias is < 0 or > 365) throw new ArgumentException("Prazo: de 0 a 365 dias.", nameof(prazoDias));
        if (tipo == TipoDePagamento.ValeTroca && (taxaAVista != 0 || taxaParcelado != 0 || prazoDias != 0 || contaId is not null))
            throw new ArgumentException("Vale-troca não tem taxa, prazo nem conta: é um crédito do cliente.", nameof(tipo));
        if (tipo == TipoDePagamento.Crediario && (taxaAVista != 0 || taxaParcelado != 0 || contaId is not null))
            throw new ArgumentException("Crediário não tem taxa nem conta: as parcelas ficam a receber do cliente.", nameof(tipo));
        (Nome, Tipo, ContaId, TaxaAVista, TaxaParcelado, PrazoDias) = (nome.Trim()[..Math.Min(nome.Trim().Length, 60)], tipo, contaId, taxaAVista, taxaParcelado, prazoDias);
        MaxParcelas = tipo is TipoDePagamento.Credito or TipoDePagamento.Crediario ? maxParcelas : 1;
    }

    public void Desativar() => Ativa = false;

    public void Reativar() => Ativa = true;

    public decimal Taxa(int parcelas) => parcelas <= 1 ? TaxaAVista : TaxaParcelado;

    /// <summary>Quanto cai e quando: valor líquido (sem a taxa) dividido nas parcelas, centavos na 1ª.</summary>
    public IReadOnlyList<ParcelaAReceber> Recebimentos(decimal valor, int parcelas, DateOnly data)
    {
        var n = Math.Max(1, parcelas);
        var liquido = Dinheiro.Arredondar(valor * (1 - Taxa(n) / 100));
        var cada = Math.Floor(liquido / n * 100) / 100;
        var resto = liquido - cada * n;
        // Crediário vence no mesmo dia de cada mês; cartão, a cada 30 dias.
        return Enumerable.Range(0, n).Select(i => new ParcelaAReceber(
            Tipo == TipoDePagamento.Crediario ? data.AddDays(PrazoDias).AddMonths(i) : data.AddDays(PrazoDias + 30 * i), i == 0 ? cada + resto : cada)).ToList();
    }
}

/// <summary>Regras de venda da empresa.</summary>
[DisplayName("Configuração de vendas")]
public sealed class ConfiguracaoDeVendas : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }

    /// <summary>Vender sem saldo deixa o estoque negativo (com aviso) em vez de travar a venda. Padrão: sim.</summary>
    public bool PermitirEstoqueNegativo { get; private set; } = true;

    /// <summary>Dias após a venda em que a troca é aceita (fora do prazo, só Dono/Gerente). Padrão: 30.</summary>
    public int PrazoDeTrocaDias { get; private set; } = 30;

    /// <summary>Validade do vale-troca em dias. Padrão: 90.</summary>
    public int ValidadeDoValeDias { get; private set; } = 90;

    /// <summary>Prazo padrão para devolver a condicional. Padrão: 2 dias.</summary>
    public int PrazoCondicionalDias { get; private set; } = 2;

    /// <summary>Peças em condicional por cliente (somando as abertas). 0 = sem limite. Padrão: 10.</summary>
    public int LimiteCondicionalPecas { get; private set; } = 10;

    /// <summary>Valor em condicional por cliente (somando as abertas). 0 = sem limite. Padrão: R$ 1.000.</summary>
    public decimal LimiteCondicionalValor { get; private set; } = 1000m;

    private ConfiguracaoDeVendas() { }

    public static ConfiguracaoDeVendas Padrao(Guid empresaId) => new() { Id = Guid.NewGuid(), EmpresaId = empresaId };

    /// <summary>Altera as regras; parâmetros nulos mantêm o valor atual.</summary>
    public void Definir(bool permitirEstoqueNegativo, int? prazoDeTrocaDias = null, int? validadeDoValeDias = null, int? prazoCondicionalDias = null,
        int? limiteCondicionalPecas = null, decimal? limiteCondicionalValor = null)
    {
        var prazo = prazoDeTrocaDias ?? PrazoDeTrocaDias;
        var validade = validadeDoValeDias ?? ValidadeDoValeDias;
        var prazoCondicional = prazoCondicionalDias ?? PrazoCondicionalDias;
        var pecas = limiteCondicionalPecas ?? LimiteCondicionalPecas;
        var valor = limiteCondicionalValor ?? LimiteCondicionalValor;
        if (prazo is < 0 or > 365) throw new ArgumentException("Prazo de troca: de 0 a 365 dias.", nameof(prazoDeTrocaDias));
        if (validade is < 1 or > 3650) throw new ArgumentException("Validade do vale-troca: de 1 a 3650 dias.", nameof(validadeDoValeDias));
        if (prazoCondicional is < 1 or > 60) throw new ArgumentException("Prazo da condicional: de 1 a 60 dias.", nameof(prazoCondicionalDias));
        if (pecas < 0 || valor < 0) throw new ArgumentException("Os limites da condicional não podem ser negativos (0 = sem limite).", nameof(limiteCondicionalPecas));
        (PermitirEstoqueNegativo, PrazoDeTrocaDias, ValidadeDoValeDias) = (permitirEstoqueNegativo, prazo, validade);
        (PrazoCondicionalDias, LimiteCondicionalPecas, LimiteCondicionalValor) = (prazoCondicional, pecas, Dinheiro.Arredondar(valor));
    }
}
