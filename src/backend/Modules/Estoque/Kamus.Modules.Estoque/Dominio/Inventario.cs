using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Estoque.Dominio;

public enum StatusDoInventario
{
    Aberto,
    Concluido,
    Cancelado,
}

public sealed record FotoDoSaldo(Guid SkuId, Guid ProdutoId, int Quantidade);

/// <summary>
/// Contagem física de uma loja. Na abertura guarda a foto do saldo; a contagem é cega (quem conta não vê o saldo).
/// Ao concluir, cada SKU recebe um ajuste pela diferença contado − foto, aplicada sobre o saldo do momento — assim,
/// entradas e saídas feitas durante a contagem não se perdem.
/// </summary>
[DisplayName("Inventário")]
public sealed class Inventario : ITenantOwned
{
    private readonly List<ItemDeInventario> _itens = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LojaId { get; private set; }
    public StatusDoInventario Status { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset AbertoEm { get; private set; }
    public string? AbertoPor { get; private set; }
    public DateTimeOffset? EncerradoEm { get; private set; }
    public string? ConcluidoPor { get; private set; }
    public bool ZerouNaoContados { get; private set; }

    /// <summary>Operação dos ajustes gerados na conclusão (kardex).</summary>
    public Guid? OperacaoId { get; private set; }

    public IReadOnlyCollection<ItemDeInventario> Itens => _itens;

    private Inventario() { }

    public static Inventario Abrir(Guid lojaId, IEnumerable<FotoDoSaldo> foto, DateTimeOffset agora, string? usuario, string? observacao)
    {
        var inventario = new Inventario
        {
            Id = Guid.NewGuid(),
            LojaId = lojaId,
            Status = StatusDoInventario.Aberto,
            AbertoEm = agora,
            AbertoPor = usuario,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()[..Math.Min(observacao.Trim().Length, 500)],
        };
        inventario._itens.AddRange(foto.Select(f => ItemDeInventario.Criar(inventario.Id, f.SkuId, f.ProdutoId, f.Quantidade)));
        return inventario;
    }

    /// <summary>Define a quantidade contada (ou soma, na leitura de código de barras). Devolve o item.</summary>
    public ItemDeInventario Contar(Guid skuId, Guid produtoId, int quantidade, bool somar, DateTimeOffset agora)
    {
        GarantirAberto();
        var item = _itens.FirstOrDefault(i => i.SkuId == skuId);
        var total = somar ? (item?.Contado ?? 0) + quantidade : quantidade;
        ArgumentOutOfRangeException.ThrowIfNegative(total, nameof(quantidade));
        if (item is null)
        {
            item = ItemDeInventario.Criar(Id, skuId, produtoId, 0);
            _itens.Add(item);
        }
        item.Registrar(total, agora);
        return item;
    }

    public void LimparContagem(Guid skuId)
    {
        GarantirAberto();
        _itens.FirstOrDefault(i => i.SkuId == skuId)?.Registrar(null, null);
    }

    /// <summary>Diferença (contado − foto) dos SKUs que mudam. Não contados contam como zero só se pedido.</summary>
    public IReadOnlyList<(Guid SkuId, int Diferenca)> Diferencas(bool zerarNaoContados) =>
        _itens.Select(i => (i.SkuId, Contado: i.Contado ?? (zerarNaoContados ? 0 : (int?)null), i.SaldoNaAbertura))
            .Where(x => x.Contado is not null && x.Contado != x.SaldoNaAbertura)
            .Select(x => (x.SkuId, x.Contado!.Value - x.SaldoNaAbertura)).ToList();

    public void Concluir(DateTimeOffset agora, string? usuario, Guid operacaoId, bool zerouNaoContados)
    {
        GarantirAberto();
        (Status, EncerradoEm, ConcluidoPor, OperacaoId, ZerouNaoContados) = (StatusDoInventario.Concluido, agora, usuario, operacaoId, zerouNaoContados);
    }

    public void Cancelar(DateTimeOffset agora, string? usuario)
    {
        GarantirAberto();
        (Status, EncerradoEm, ConcluidoPor) = (StatusDoInventario.Cancelado, agora, usuario);
    }

    private void GarantirAberto()
    {
        if (Status != StatusDoInventario.Aberto) throw new InvalidOperationException("Este inventário já foi encerrado.");
    }
}

/// <summary>Foto do saldo e contagem de um SKU no inventário (sem histórico próprio: o inventário inteiro é o registro).</summary>
[DisplayName("Item de inventário"), NaoAuditar]
public sealed class ItemDeInventario : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InventarioId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public int SaldoNaAbertura { get; private set; }
    public int? Contado { get; private set; }
    public DateTimeOffset? ContadoEm { get; private set; }

    private ItemDeInventario() { }

    internal static ItemDeInventario Criar(Guid inventarioId, Guid skuId, Guid produtoId, int saldo) =>
        new() { Id = Guid.NewGuid(), InventarioId = inventarioId, SkuId = skuId, ProdutoId = produtoId, SaldoNaAbertura = saldo };

    internal void Registrar(int? contado, DateTimeOffset? agora) => (Contado, ContadoEm) = (contado, agora);
}
