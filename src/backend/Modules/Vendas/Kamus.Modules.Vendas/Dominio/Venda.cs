using System.ComponentModel;
using System.Globalization;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

public enum StatusDaVenda
{
    Concluida,
    Cancelada,
}

public sealed record DadosDaVenda(Guid EmpresaId, Guid LojaId, int Numero, DateOnly Data, Guid? ClienteId, string? ClienteNome, Guid? VendedorId, string? VendedorNome,
    decimal ComissaoPercentual, string? NotaExterna, string? Observacao);

/// <param name="Desconto">Desconto em reais na linha.</param>
public sealed record ItemNovo(Guid SkuId, Guid ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Desconto);

public sealed record PagamentoNovo(Guid FormaId, string FormaNome, TipoDePagamento Tipo, decimal Valor, int Parcelas, decimal TaxaPercentual);

/// <summary>
/// Venda registrada no backoffice (sem emissão fiscal: a nota sai no sistema atual da loja e o número fica em <see cref="NotaExterna"/>).
/// Baixa o estoque e gera os recebimentos; o cancelamento devolve o estoque e estorna o financeiro.
/// </summary>
[DisplayName("Venda")]
public sealed class Venda : ITenantOwned
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly List<ItemDaVenda> _itens = [];
    private readonly List<PagamentoDaVenda> _pagamentos = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid LojaId { get; private set; }
    public int Numero { get; private set; }
    public DateOnly Data { get; private set; }
    public Guid? ClienteId { get; private set; }
    public string? ClienteNome { get; private set; }
    public Guid? VendedorId { get; private set; }
    public string? VendedorNome { get; private set; }
    public decimal ComissaoPercentual { get; private set; }
    public decimal DescontoGeral { get; private set; }
    public decimal Troco { get; private set; }
    public string? NotaExterna { get; private set; }
    public string? Observacao { get; private set; }
    public StatusDaVenda Status { get; private set; }
    public string? RegistradaPor { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }
    public Guid? OperacaoDeEstoqueId { get; private set; }
    public string? MotivoDoCancelamento { get; private set; }
    public string? CanceladaPor { get; private set; }
    public DateTimeOffset? CanceladaEm { get; private set; }

    public IReadOnlyCollection<ItemDaVenda> Itens => _itens;
    public IReadOnlyCollection<PagamentoDaVenda> Pagamentos => _pagamentos;

    public decimal Subtotal => _itens.Sum(i => i.Total);
    public decimal Total => Subtotal - DescontoGeral;
    public int Pecas => _itens.Sum(i => i.Quantidade);
    public decimal Comissao => Math.Round(Total * ComissaoPercentual / 100, 2);
    public decimal TaxaTotal => _pagamentos.Sum(p => p.ValorTaxa);
    public decimal CustoTotal => _itens.Sum(i => i.CustoUnitario * i.Quantidade);
    public decimal LucroBruto => Total - CustoTotal - TaxaTotal;

    private Venda() { }

    private static string Moeda(decimal v) => v.ToString("N2", PtBr);

    public static Venda Criar(DadosDaVenda dados, IReadOnlyList<ItemNovo> itens, decimal descontoGeral, IReadOnlyList<PagamentoNovo> pagamentos, string? usuario, DateTimeOffset agora)
    {
        if (itens.Count == 0) throw new ArgumentException("Adicione ao menos um item.", nameof(itens));
        if (itens.Any(i => i.Quantidade <= 0)) throw new ArgumentException("As quantidades precisam ser maiores que zero.", nameof(itens));
        if (itens.Any(i => i.PrecoUnitario < 0 || i.Desconto < 0)) throw new ArgumentException("Preço e desconto não podem ser negativos.", nameof(itens));
        if (itens.Any(i => i.Desconto > i.Quantidade * i.PrecoUnitario)) throw new ArgumentException("O desconto de um item passa do valor dele.", nameof(itens));
        if (dados.ComissaoPercentual is < 0 or > 100) throw new ArgumentException("Comissão inválida.", nameof(dados));

        var venda = new Venda
        {
            Id = Guid.NewGuid(),
            EmpresaId = dados.EmpresaId,
            LojaId = dados.LojaId,
            Numero = dados.Numero,
            Data = dados.Data,
            ClienteId = dados.ClienteId,
            ClienteNome = Limitar(dados.ClienteNome, 150),
            VendedorId = dados.VendedorId,
            VendedorNome = Limitar(dados.VendedorNome, 150),
            ComissaoPercentual = dados.ComissaoPercentual,
            NotaExterna = Limitar(dados.NotaExterna, 60),
            Observacao = Limitar(dados.Observacao, 500),
            Status = StatusDaVenda.Concluida,
            RegistradaPor = usuario,
            RegistradaEm = agora,
        };
        venda._itens.AddRange(itens.Select(i => ItemDaVenda.De(venda.Id, i)));
        if (descontoGeral < 0 || descontoGeral > venda.Subtotal) throw new ArgumentException("O desconto da venda passa do subtotal.", nameof(descontoGeral));
        venda.DescontoGeral = Math.Round(descontoGeral, 2);

        if (pagamentos.Any(p => p.Valor <= 0 || p.Parcelas < 1)) throw new ArgumentException("Cada pagamento precisa de valor e de ao menos 1 parcela.", nameof(pagamentos));
        var pago = pagamentos.Sum(p => p.Valor);
        if (pago < venda.Total) throw new ArgumentException($"Os pagamentos somam R$ {Moeda(pago)}: faltam R$ {Moeda(venda.Total - pago)}.", nameof(pagamentos));
        var troco = pago - venda.Total;
        var dinheiro = pagamentos.Where(p => p.Tipo == TipoDePagamento.Dinheiro).Sum(p => p.Valor);
        if (troco > dinheiro)
            throw new ArgumentException($"Os pagamentos passam do total em R$ {Moeda(troco)}: só pagamento em dinheiro pode ter troco.", nameof(pagamentos));
        venda.Troco = troco;

        var trocoAAbater = troco;
        foreach (var p in pagamentos)
        {
            var valor = p.Valor;
            if (p.Tipo == TipoDePagamento.Dinheiro && trocoAAbater > 0)
            {
                var abatido = Math.Min(valor, trocoAAbater);
                valor -= abatido;
                trocoAAbater -= abatido;
            }
            if (valor > 0) venda._pagamentos.Add(PagamentoDaVenda.De(venda.Id, p with { Valor = valor }));
        }
        return venda;
    }

    /// <summary>Guarda a operação de estoque (para devolver no cancelamento) e o custo médio de cada SKU no momento da venda.</summary>
    public void RegistrarBaixaDeEstoque(Guid operacaoId, IReadOnlyDictionary<Guid, decimal> custos)
    {
        OperacaoDeEstoqueId = operacaoId;
        foreach (var item in _itens) item.DefinirCusto(custos.GetValueOrDefault(item.SkuId));
    }

    public void Cancelar(string motivo, string? usuario, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("Informe o motivo do cancelamento.", nameof(motivo));
        if (Status == StatusDaVenda.Cancelada) throw new InvalidOperationException("Esta venda já foi cancelada.");
        (Status, MotivoDoCancelamento, CanceladaPor, CanceladaEm) = (StatusDaVenda.Cancelada, Limitar(motivo, 300), usuario, agora);
    }

    private static string? Limitar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, maximo)];
}

[DisplayName("Item da venda"), NaoAuditar]
public sealed class ItemDaVenda : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VendaId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = "";
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Desconto { get; private set; }

    /// <summary>Custo médio do SKU no momento da venda (base do lucro bruto).</summary>
    public decimal CustoUnitario { get; private set; }

    public decimal Total => Quantidade * PrecoUnitario - Desconto;

    private ItemDaVenda() { }

    internal static ItemDaVenda De(Guid vendaId, ItemNovo i) => new()
    {
        Id = Guid.NewGuid(),
        VendaId = vendaId,
        SkuId = i.SkuId,
        ProdutoId = i.ProdutoId,
        Descricao = i.Descricao[..Math.Min(i.Descricao.Length, 200)],
        Quantidade = i.Quantidade,
        PrecoUnitario = Math.Round(i.PrecoUnitario, 2),
        Desconto = Math.Round(i.Desconto, 2),
    };

    internal void DefinirCusto(decimal custo) => CustoUnitario = custo;
}

[DisplayName("Pagamento da venda"), NaoAuditar]
public sealed class PagamentoDaVenda : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VendaId { get; private set; }
    public Guid FormaId { get; private set; }
    public string FormaNome { get; private set; } = "";
    public TipoDePagamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public int Parcelas { get; private set; }
    public decimal TaxaPercentual { get; private set; }

    public decimal ValorTaxa => Math.Round(Valor * TaxaPercentual / 100, 2);

    private PagamentoDaVenda() { }

    internal static PagamentoDaVenda De(Guid vendaId, PagamentoNovo p) => new()
    {
        Id = Guid.NewGuid(),
        VendaId = vendaId,
        FormaId = p.FormaId,
        FormaNome = p.FormaNome,
        Tipo = p.Tipo,
        Valor = Math.Round(p.Valor, 2),
        Parcelas = p.Parcelas,
        TaxaPercentual = p.TaxaPercentual,
    };
}
