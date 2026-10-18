using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Vendas.Dominio;

/// <summary>
/// Fechamento da comissão de um vendedor num período: junta as vendas concluídas e os ajustes de troca ainda não fechados,
/// marca-os (não entram de novo) e pode gerar a conta a pagar ao vendedor. Reabrir desfaz a marcação.
/// </summary>
[DisplayName("Fechamento de comissão")]
public sealed class FechamentoDeComissao : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid VendedorId { get; private set; }
    public string VendedorNome { get; private set; } = "";
    public DateOnly De { get; private set; }
    public DateOnly Ate { get; private set; }
    public int Vendas { get; private set; }
    public int Trocas { get; private set; }

    /// <summary>Total das vendas mais a diferença das trocas (base da comissão).</summary>
    public decimal TotalVendido { get; private set; }

    public decimal Comissao { get; private set; }
    public string? FechadoPor { get; private set; }
    public DateTimeOffset FechadoEm { get; private set; }
    public bool ContaAPagarGerada { get; private set; }

    private FechamentoDeComissao() { }

    public static FechamentoDeComissao Fechar(Guid empresaId, Guid vendedorId, string vendedorNome, DateOnly de, DateOnly ate,
        IReadOnlyList<Venda> vendas, IReadOnlyList<Troca> trocas, string? usuario, DateTimeOffset agora)
    {
        if (ate < de) throw new ArgumentException("O período termina antes de começar.", nameof(ate));
        if (vendas.Count == 0 && trocas.Count == 0) throw new ArgumentException("Nada a fechar: não há vendas nem trocas abertas desse vendedor no período.", nameof(vendas));
        var comissao = vendas.Sum(v => v.Comissao) + trocas.Sum(t => t.Comissao);
        if (comissao < 0) throw new ArgumentException($"A comissão do período ficaria negativa (R$ {Dinheiro.Formatar(comissao)}) por causa de trocas: feche junto com novas vendas.", nameof(trocas));

        var fechamento = new FechamentoDeComissao
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            VendedorId = vendedorId,
            VendedorNome = vendedorNome,
            De = de,
            Ate = ate,
            Vendas = vendas.Count,
            Trocas = trocas.Count,
            TotalVendido = vendas.Sum(v => v.Total) + trocas.Sum(t => t.Diferenca),
            Comissao = comissao,
            FechadoPor = usuario,
            FechadoEm = agora,
        };
        foreach (var v in vendas) v.MarcarFechamento(fechamento.Id);
        foreach (var t in trocas) t.MarcarFechamento(fechamento.Id);
        return fechamento;
    }

    public void RegistrarContaAPagar() => ContaAPagarGerada = true;

    /// <summary>Solta as vendas e trocas para entrarem num novo fechamento (o registro do fechamento é removido por quem chama).</summary>
    public static void Reabrir(IEnumerable<Venda> vendas, IEnumerable<Troca> trocas)
    {
        foreach (var v in vendas) v.MarcarFechamento(null);
        foreach (var t in trocas) t.MarcarFechamento(null);
    }
}
