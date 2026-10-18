using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

public enum MotivoDaTroca
{
    Tamanho,
    Cor,
    Defeito,
    Desistencia,
    Outro,
}

/// <summary>Para onde vai a peça devolvida: volta à venda ou fica separada como avariada (não volta ao estoque disponível).</summary>
public enum DestinoDaDevolucao
{
    Estoque,
    Avariado,
}

public sealed record DadosDaTroca(Guid EmpresaId, Guid LojaId, int Numero, DateOnly Data, MotivoDaTroca Motivo, string? Observacao, Guid? RegistradaPorId);

public sealed record DevolucaoNova(Guid SkuId, int Quantidade, DestinoDaDevolucao Destino);

/// <summary>
/// Troca vinculada a uma venda: peças devolvidas (pelo valor que o cliente pagou, com o desconto da venda rateado) e peças levadas.
/// Se o cliente leva mais, paga a diferença; se leva menos (ou nada), a diferença vira vale-troca. A comissão do vendedor da venda
/// original é ajustada pela diferença.
/// </summary>
[DisplayName("Troca")]
public sealed class Troca : ITenantOwned
{
    private readonly List<ItemDevolvidoNaTroca> _devolvidos = [];
    private readonly List<ItemLevadoNaTroca> _levados = [];
    private readonly List<PagamentoDaTroca> _pagamentos = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid LojaId { get; private set; }
    public int Numero { get; private set; }
    public DateOnly Data { get; private set; }
    public Guid VendaId { get; private set; }
    public int VendaNumero { get; private set; }
    public Guid? ClienteId { get; private set; }
    public string? ClienteNome { get; private set; }
    public Guid? VendedorId { get; private set; }
    public string? VendedorNome { get; private set; }
    public decimal ComissaoPercentual { get; private set; }
    public MotivoDaTroca Motivo { get; private set; }
    public string? Observacao { get; private set; }
    public decimal Troco { get; private set; }
    public Guid? ValeId { get; private set; }
    public Guid? OperacaoDeDevolucaoId { get; private set; }
    public Guid? OperacaoDeSaidaId { get; private set; }
    public Guid? FechamentoId { get; private set; }
    public Guid? RegistradaPorId { get; private set; }
    public string? RegistradaPor { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }

    public IReadOnlyCollection<ItemDevolvidoNaTroca> Devolvidos => _devolvidos;
    public IReadOnlyCollection<ItemLevadoNaTroca> Levados => _levados;
    public IReadOnlyCollection<PagamentoDaTroca> Pagamentos => _pagamentos;

    public decimal ValorDevolvido => _devolvidos.Sum(d => d.Valor);
    public decimal ValorLevado => _levados.Sum(l => l.Total);

    /// <summary>Positivo: o cliente pagou; negativo: crédito do cliente (vale-troca).</summary>
    public decimal Diferenca => ValorLevado - ValorDevolvido;

    public decimal ValorDoVale => Math.Max(0, -Diferenca);
    public decimal Comissao => Dinheiro.Arredondar(Diferenca * ComissaoPercentual / 100);
    public decimal TaxaTotal => _pagamentos.Sum(p => p.ValorTaxa);

    private Troca() { }

    /// <param name="jaDevolvidos">Peças de cada SKU desta venda já devolvidas em trocas anteriores.</param>
    public static Troca Criar(DadosDaTroca dados, Venda venda, IReadOnlyList<DevolucaoNova> devolvidos, IReadOnlyList<ItemNovo> levados,
        IReadOnlyList<PagamentoNovo> pagamentos, IReadOnlyDictionary<Guid, int> jaDevolvidos, string? usuario, DateTimeOffset agora)
    {
        if (venda.Status == StatusDaVenda.Cancelada) throw new InvalidOperationException($"A venda nº {venda.Numero} foi cancelada: não há o que trocar.");
        if (!Enum.IsDefined(dados.Motivo)) throw new ArgumentException("Motivo inválido.", nameof(dados));
        if (devolvidos.Count == 0) throw new ArgumentException("Escolha ao menos uma peça devolvida.", nameof(devolvidos));
        if (devolvidos.Any(d => d.Quantidade <= 0 || !Enum.IsDefined(d.Destino))) throw new ArgumentException("As quantidades devolvidas precisam ser maiores que zero.", nameof(devolvidos));
        if (levados.Any(i => i.Quantidade <= 0 || i.PrecoUnitario < 0 || i.Desconto < 0 || i.Desconto > i.Quantidade * i.PrecoUnitario))
            throw new ArgumentException("Confira as quantidades e os descontos das peças levadas.", nameof(levados));

        var troca = new Troca
        {
            Id = Guid.NewGuid(),
            EmpresaId = dados.EmpresaId,
            LojaId = dados.LojaId,
            Numero = dados.Numero,
            Data = dados.Data,
            VendaId = venda.Id,
            VendaNumero = venda.Numero,
            ClienteId = venda.ClienteId,
            ClienteNome = venda.ClienteNome,
            VendedorId = venda.VendedorId,
            VendedorNome = venda.VendedorNome,
            ComissaoPercentual = venda.ComissaoPercentual,
            Motivo = dados.Motivo,
            Observacao = Venda.Limitar(dados.Observacao, 500),
            RegistradaPorId = dados.RegistradaPorId,
            RegistradaPor = usuario,
            RegistradaEm = agora,
        };

        var pagos = venda.ValoresPagosPorSku();
        foreach (var porSku in devolvidos.GroupBy(d => (d.SkuId, d.Destino)))
        {
            var item = venda.Itens.FirstOrDefault(i => i.SkuId == porSku.Key.SkuId)
                ?? throw new ArgumentException("Há peça devolvida que não está na venda.", nameof(devolvidos));
            var (vendidas, valorPago) = pagos[item.SkuId];
            var antes = jaDevolvidos.GetValueOrDefault(item.SkuId) + troca._devolvidos.Where(d => d.SkuId == item.SkuId).Sum(d => d.Quantidade);
            var quantidade = porSku.Sum(d => d.Quantidade);
            if (antes + quantidade > vendidas)
                throw new ArgumentException(antes == 0
                    ? $"{item.Descricao}: a venda tem {vendidas} e a troca devolve {quantidade}."
                    : $"{item.Descricao}: resta {vendidas - antes} para devolver (a venda tem {vendidas}).", nameof(devolvidos));
            // Rateio pelas peças: devolver todas as peças de um SKU devolve exatamente o que foi pago por ele.
            var valor = Dinheiro.Arredondar(valorPago * (antes + quantidade) / vendidas) - Dinheiro.Arredondar(valorPago * antes / vendidas);
            troca._devolvidos.Add(ItemDevolvidoNaTroca.De(troca.Id, item, quantidade, valor, porSku.Key.Destino));
        }

        troca._levados.AddRange(levados.Select(i => ItemLevadoNaTroca.De(troca.Id, i)));
        if (troca.Diferenca > 0)
        {
            var (gravados, troco) = Dinheiro.Distribuir(troca.Diferenca, pagamentos);
            troca.Troco = troco;
            troca._pagamentos.AddRange(gravados.Select(p => PagamentoDaTroca.De(troca.Id, p)));
        }
        else if (pagamentos.Count > 0)
            throw new ArgumentException("O cliente não tem diferença a pagar: o crédito dele vira vale-troca.", nameof(pagamentos));
        return troca;
    }

    /// <summary>Operações de estoque (para auditoria) e custo médio das peças levadas.</summary>
    public void RegistrarEstoque(Guid? devolucaoId, Guid? saidaId, IReadOnlyDictionary<Guid, decimal> custosDosLevados)
    {
        (OperacaoDeDevolucaoId, OperacaoDeSaidaId) = (devolucaoId, saidaId);
        foreach (var item in _levados) item.DefinirCusto(custosDosLevados.GetValueOrDefault(item.SkuId));
    }

    public void VincularVale(Guid valeId) => ValeId = valeId;

    internal void MarcarFechamento(Guid? fechamentoId) => FechamentoId = fechamentoId;
}

[DisplayName("Peça devolvida na troca"), NaoAuditar]
public sealed class ItemDevolvidoNaTroca : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TrocaId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = "";
    public int Quantidade { get; private set; }

    /// <summary>Quanto o cliente pagou por estas peças (vira crédito na troca).</summary>
    public decimal Valor { get; private set; }

    /// <summary>Custo médio gravado na venda original (é com ele que a peça volta ao estoque).</summary>
    public decimal CustoUnitario { get; private set; }

    public DestinoDaDevolucao Destino { get; private set; }

    private ItemDevolvidoNaTroca() { }

    internal static ItemDevolvidoNaTroca De(Guid trocaId, ItemDaVenda item, int quantidade, decimal valor, DestinoDaDevolucao destino) => new()
    {
        Id = Guid.NewGuid(),
        TrocaId = trocaId,
        SkuId = item.SkuId,
        ProdutoId = item.ProdutoId,
        Descricao = item.Descricao,
        Quantidade = quantidade,
        Valor = valor,
        CustoUnitario = item.CustoUnitario,
        Destino = destino,
    };
}

[DisplayName("Peça levada na troca"), NaoAuditar]
public sealed class ItemLevadoNaTroca : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TrocaId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = "";
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Desconto { get; private set; }
    public decimal CustoUnitario { get; private set; }

    public decimal Total => Quantidade * PrecoUnitario - Desconto;

    private ItemLevadoNaTroca() { }

    internal static ItemLevadoNaTroca De(Guid trocaId, ItemNovo i) => new()
    {
        Id = Guid.NewGuid(),
        TrocaId = trocaId,
        SkuId = i.SkuId,
        ProdutoId = i.ProdutoId,
        Descricao = i.Descricao[..Math.Min(i.Descricao.Length, 200)],
        Quantidade = i.Quantidade,
        PrecoUnitario = Dinheiro.Arredondar(i.PrecoUnitario),
        Desconto = Dinheiro.Arredondar(i.Desconto),
    };

    internal void DefinirCusto(decimal custo) => CustoUnitario = custo;
}

[DisplayName("Pagamento da troca"), NaoAuditar]
public sealed class PagamentoDaTroca : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TrocaId { get; private set; }
    public Guid FormaId { get; private set; }
    public string FormaNome { get; private set; } = "";
    public TipoDePagamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public int Parcelas { get; private set; }
    public decimal TaxaPercentual { get; private set; }
    public Guid? ValeId { get; private set; }

    public decimal ValorTaxa => Dinheiro.Arredondar(Valor * TaxaPercentual / 100);

    private PagamentoDaTroca() { }

    internal static PagamentoDaTroca De(Guid trocaId, PagamentoNovo p) => new()
    {
        Id = Guid.NewGuid(),
        TrocaId = trocaId,
        FormaId = p.FormaId,
        FormaNome = p.FormaNome,
        Tipo = p.Tipo,
        Valor = Dinheiro.Arredondar(p.Valor),
        Parcelas = p.Parcelas,
        TaxaPercentual = p.TaxaPercentual,
        ValeId = p.ValeId,
    };
}
