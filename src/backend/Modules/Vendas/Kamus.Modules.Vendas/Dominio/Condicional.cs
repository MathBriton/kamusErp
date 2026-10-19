using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

public enum SituacaoDaCondicional
{
    Aberta,
    Atrasada,
    Fechada,
}

public sealed record DadosDaCondicional(Guid EmpresaId, Guid LojaId, int Numero, DateOnly Data, DateOnly Prazo, Guid ClienteId, string ClienteNome,
    Guid? VendedorId, string? VendedorNome, string? Observacao, Guid? RegistradaPorId);

/// <summary>Peça que o cliente ficou (vira item da venda que fecha a condicional).</summary>
public sealed record PecaQueFicou(Guid SkuId, Guid ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario);

/// <summary>
/// Condicional (malinha): peças que o cliente leva para provar, fora do estoque disponível até a devolução. No fechamento, o que
/// volta entra no estoque e o que ficou vira venda.
/// </summary>
[DisplayName("Condicional")]
public sealed class Condicional : ITenantOwned
{
    private readonly List<ItemDaCondicional> _itens = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid LojaId { get; private set; }
    public int Numero { get; private set; }
    public DateOnly Data { get; private set; }
    public DateOnly Prazo { get; private set; }
    public Guid ClienteId { get; private set; }
    public string ClienteNome { get; private set; } = "";
    public Guid? VendedorId { get; private set; }
    public string? VendedorNome { get; private set; }
    public string? Observacao { get; private set; }
    public Guid? OperacaoDeSaidaId { get; private set; }
    public bool Fechada { get; private set; }
    public DateTimeOffset? FechadaEm { get; private set; }
    public string? FechadaPor { get; private set; }
    public Guid? VendaId { get; private set; }
    public int? VendaNumero { get; private set; }
    public Guid? RegistradaPorId { get; private set; }
    public string? RegistradaPor { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }

    public IReadOnlyCollection<ItemDaCondicional> Itens => _itens;

    public int Pecas => _itens.Sum(i => i.Quantidade);
    public decimal Valor => _itens.Sum(i => i.Quantidade * i.PrecoUnitario);
    public int PecasVendidas => Fechada ? _itens.Sum(i => i.Quantidade - i.Devolvidas) : 0;
    public decimal ValorVendido => Fechada ? _itens.Sum(i => (i.Quantidade - i.Devolvidas) * i.PrecoUnitario) : 0;

    private Condicional() { }

    public SituacaoDaCondicional Situacao(DateOnly hoje) =>
        Fechada ? SituacaoDaCondicional.Fechada : hoje > Prazo ? SituacaoDaCondicional.Atrasada : SituacaoDaCondicional.Aberta;

    public static Condicional Abrir(DadosDaCondicional dados, IReadOnlyList<ItemNovo> itens, string? usuario, DateTimeOffset agora)
    {
        if (itens.Count == 0) throw new ArgumentException("Escolha ao menos uma peça.", nameof(itens));
        if (itens.Any(i => i.Quantidade <= 0)) throw new ArgumentException("As quantidades precisam ser maiores que zero.", nameof(itens));
        if (dados.Prazo < dados.Data) throw new ArgumentException("O prazo de devolução não pode ser antes da saída.", nameof(dados));
        var c = new Condicional
        {
            Id = Guid.NewGuid(),
            EmpresaId = dados.EmpresaId,
            LojaId = dados.LojaId,
            Numero = dados.Numero,
            Data = dados.Data,
            Prazo = dados.Prazo,
            ClienteId = dados.ClienteId,
            ClienteNome = Venda.Limitar(dados.ClienteNome, 150) ?? "",
            VendedorId = dados.VendedorId,
            VendedorNome = Venda.Limitar(dados.VendedorNome, 150),
            Observacao = Venda.Limitar(dados.Observacao, 500),
            RegistradaPorId = dados.RegistradaPorId,
            RegistradaPor = usuario,
            RegistradaEm = agora,
        };
        foreach (var g in itens.GroupBy(i => i.SkuId))
            c._itens.Add(ItemDaCondicional.De(c.Id, g.First() with { Quantidade = g.Sum(i => i.Quantidade) }));
        return c;
    }

    public void RegistrarSaida(Guid operacaoId, IReadOnlyDictionary<Guid, decimal> custos)
    {
        OperacaoDeSaidaId = operacaoId;
        foreach (var i in _itens) i.DefinirCusto(custos.GetValueOrDefault(i.SkuId));
    }

    /// <summary>Confere as devolvidas e devolve o que ficou com o cliente.</summary>
    public IReadOnlyList<PecaQueFicou> Conferir(IReadOnlyDictionary<Guid, int> devolvidas)
    {
        if (Fechada) throw new InvalidOperationException($"A condicional nº {Numero} já foi fechada.");
        if (devolvidas.Keys.Any(k => _itens.All(i => i.SkuId != k))) throw new ArgumentException("Há peça devolvida que não está na condicional.", nameof(devolvidas));
        foreach (var i in _itens)
        {
            var d = devolvidas.GetValueOrDefault(i.SkuId);
            if (d < 0 || d > i.Quantidade) throw new ArgumentException($"{i.Descricao}: o cliente levou {i.Quantidade} e a devolução tem {d}.", nameof(devolvidas));
        }
        return _itens.Select(i => new PecaQueFicou(i.SkuId, i.ProdutoId, i.Descricao, i.Quantidade - devolvidas.GetValueOrDefault(i.SkuId), i.PrecoUnitario))
            .Where(p => p.Quantidade > 0).ToList();
    }

    public void Fechar(IReadOnlyDictionary<Guid, int> devolvidas, Guid? vendaId, int? vendaNumero, string? usuario, DateTimeOffset agora)
    {
        var ficou = Conferir(devolvidas);
        if (ficou.Count > 0 && vendaId is null) throw new ArgumentException("As peças que o cliente ficou precisam virar venda.", nameof(vendaId));
        foreach (var i in _itens) i.Devolver(devolvidas.GetValueOrDefault(i.SkuId));
        (Fechada, FechadaEm, FechadaPor, VendaId, VendaNumero) = (true, agora, usuario, ficou.Count > 0 ? vendaId : null, ficou.Count > 0 ? vendaNumero : null);
    }

    /// <summary>Limites por cliente somando as condicionais abertas; devolve o motivo da recusa ou nulo.</summary>
    public static string? ConferirLimites(ConfiguracaoDeVendas config, int pecasAbertas, decimal valorAberto, int pecasNovas, decimal valorNovo)
    {
        if (config.LimiteCondicionalPecas > 0 && pecasAbertas + pecasNovas > config.LimiteCondicionalPecas)
            return $"O cliente ficaria com {pecasAbertas + pecasNovas} peças em condicional (limite de {config.LimiteCondicionalPecas}).";
        if (config.LimiteCondicionalValor > 0 && valorAberto + valorNovo > config.LimiteCondicionalValor)
            return $"O cliente ficaria com R$ {Dinheiro.Formatar(valorAberto + valorNovo)} em condicional (limite de R$ {Dinheiro.Formatar(config.LimiteCondicionalValor)}).";
        return null;
    }
}

[DisplayName("Peça da condicional"), NaoAuditar]
public sealed class ItemDaCondicional : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CondicionalId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = "";
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }

    /// <summary>Custo médio na saída (é com ele que a peça volta ao estoque).</summary>
    public decimal CustoUnitario { get; private set; }

    public int Devolvidas { get; private set; }

    private ItemDaCondicional() { }

    internal static ItemDaCondicional De(Guid condicionalId, ItemNovo i) => new()
    {
        Id = Guid.NewGuid(),
        CondicionalId = condicionalId,
        SkuId = i.SkuId,
        ProdutoId = i.ProdutoId,
        Descricao = i.Descricao[..Math.Min(i.Descricao.Length, 200)],
        Quantidade = i.Quantidade,
        PrecoUnitario = Dinheiro.Arredondar(i.PrecoUnitario),
    };

    internal void DefinirCusto(decimal custo) => CustoUnitario = custo;

    internal void Devolver(int quantidade) => Devolvidas = quantidade;
}
