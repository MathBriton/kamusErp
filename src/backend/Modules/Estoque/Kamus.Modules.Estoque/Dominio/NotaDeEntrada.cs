using System.ComponentModel;
using Kamus.Modules.Estoque.NotasFiscais;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Estoque.Dominio;

public enum StatusDaNota
{
    Pendente,
    Lancada,
    Descartada,
}

public enum SituacaoDoItem
{
    /// <summary>Ainda sem SKU (ou distribuição incompleta).</summary>
    Pendente,
    Pronto,

    /// <summary>Não entra no estoque (ex.: embalagem, frete cobrado como item).</summary>
    Ignorado,
}

/// <summary>
/// XML da NF-e do fornecedor em conferência: cada item precisa virar SKU(s) — direto pelo de-para ou pelo EAN, ou distribuído
/// na grade (cor × tamanho) — ou ser ignorado. Lançar gera a entrada de estoque com o custo de aquisição de cada item.
/// </summary>
[DisplayName("Nota de entrada")]
public sealed class NotaDeEntrada : ITenantOwned
{
    private readonly List<ItemDaNota> _itens = [];
    private readonly List<DuplicataDaNota> _duplicatas = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Chave { get; private set; } = "";
    public string Numero { get; private set; } = "";
    public string Serie { get; private set; } = "";
    public DateTimeOffset EmitidaEm { get; private set; }
    public string FornecedorDocumento { get; private set; } = "";
    public string FornecedorNome { get; private set; } = "";
    public decimal ValorTotal { get; private set; }
    public bool Autorizada { get; private set; }
    public StatusDaNota Status { get; private set; }
    public DateTimeOffset ImportadaEm { get; private set; }
    public string? ImportadaPor { get; private set; }
    public DateTimeOffset? LancadaEm { get; private set; }
    public string? LancadaPor { get; private set; }
    public Guid? LojaId { get; private set; }
    public Guid? OperacaoId { get; private set; }
    public Guid? FornecedorId { get; private set; }

    /// <summary>XML original (a nota é o documento fiscal; guardado como veio).</summary>
    [NaoAuditar]
    public string Xml { get; private set; } = "";

    public IReadOnlyCollection<ItemDaNota> Itens => _itens;
    public IReadOnlyCollection<DuplicataDaNota> Duplicatas => _duplicatas;

    public bool ProntaParaLancar => Status == StatusDaNota.Pendente
        && _itens.All(i => i.Situacao != SituacaoDoItem.Pendente) && _itens.Any(i => i.Situacao == SituacaoDoItem.Pronto);

    private NotaDeEntrada() { }

    public static NotaDeEntrada Importar(Guid empresaId, NfeLida nfe, string xml, DateTimeOffset agora, string? usuario)
    {
        var nota = new NotaDeEntrada
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            Chave = nfe.Chave,
            Numero = nfe.Numero,
            Serie = nfe.Serie,
            EmitidaEm = nfe.EmitidaEm,
            FornecedorDocumento = nfe.Emitente.Documento,
            FornecedorNome = nfe.Emitente.NomeFantasia ?? nfe.Emitente.RazaoSocial,
            ValorTotal = nfe.ValorTotal,
            Autorizada = nfe.Autorizada,
            Status = StatusDaNota.Pendente,
            ImportadaEm = agora,
            ImportadaPor = usuario,
            Xml = xml,
        };
        nota._itens.AddRange(nfe.Itens.Select(i => ItemDaNota.De(nota, i)));
        nota._duplicatas.AddRange(nfe.Duplicatas.Select(d => DuplicataDaNota.De(nota.Id, d)));
        return nota;
    }

    public void MarcarLancada(Guid lojaId, Guid operacaoId, Guid? fornecedorId, DateTimeOffset agora, string? usuario)
    {
        if (!ProntaParaLancar) throw new InvalidOperationException("A nota ainda tem itens sem produto (vincule ou ignore cada item).");
        (Status, LojaId, OperacaoId, FornecedorId, LancadaEm, LancadaPor) = (StatusDaNota.Lancada, lojaId, operacaoId, fornecedorId, agora, usuario);
    }

    public void Descartar()
    {
        GarantirPendente();
        Status = StatusDaNota.Descartada;
    }

    internal void GarantirPendente()
    {
        if (Status != StatusDaNota.Pendente) throw new InvalidOperationException("Esta nota já foi lançada ou descartada.");
    }
}

[DisplayName("Item da nota de entrada"), NaoAuditar]
public sealed class ItemDaNota : ITenantOwned
{
    private readonly List<DistribuicaoDoItem> _distribuicao = [];

    // Referência para checar a situação da nota (não mapeada).
    private NotaDeEntrada? _nota;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotaId { get; private set; }
    public int Numero { get; private set; }
    public string Codigo { get; private set; } = "";
    public string? Ean { get; private set; }
    public string Descricao { get; private set; } = "";
    public string? Ncm { get; private set; }
    public string Unidade { get; private set; } = "";
    public decimal Quantidade { get; private set; }
    public decimal ValorTotal { get; private set; }
    public decimal CustoTotal { get; private set; }

    /// <summary>Peças por unidade da nota (ex.: caixa com 12 = 12).</summary>
    public int Fator { get; private set; } = 1;

    public Guid? ProdutoId { get; private set; }
    public bool Ignorado { get; private set; }

    /// <summary>Como o vínculo foi feito automaticamente ("de-para" ou "ean"); nulo se manual.</summary>
    public string? OrigemDoVinculo { get; private set; }

    public IReadOnlyCollection<DistribuicaoDoItem> Distribuicao => _distribuicao;

    public decimal PecasEsperadas => Quantidade * Fator;

    public SituacaoDoItem Situacao => Ignorado ? SituacaoDoItem.Ignorado
        : _distribuicao.Count > 0 && _distribuicao.Sum(d => d.Quantidade) == PecasEsperadas ? SituacaoDoItem.Pronto
        : SituacaoDoItem.Pendente;

    public decimal CustoPorPeca => PecasEsperadas > 0 ? Math.Round(CustoTotal / PecasEsperadas, 4) : 0;

    private ItemDaNota() { }

    internal static ItemDaNota De(NotaDeEntrada nota, ItemDaNfe i) => new()
    {
        Id = Guid.NewGuid(),
        NotaId = nota.Id,
        _nota = nota,
        Numero = i.Numero,
        Codigo = i.Codigo,
        Ean = i.Ean,
        Descricao = i.Descricao,
        Ncm = i.Ncm,
        Unidade = i.Unidade,
        Quantidade = i.Quantidade,
        ValorTotal = i.ValorTotal,
        CustoTotal = i.CustoTotal,
    };

    /// <summary>Liga o item a SKU(s) do produto. A soma das quantidades precisa ser a quantidade da nota × fator.</summary>
    /// <returns>As linhas novas da distribuição (as antigas saem da coleção).</returns>
    public IReadOnlyList<DistribuicaoDoItem> Distribuir(Guid produtoId, int fator, IReadOnlyList<(Guid SkuId, int Quantidade)> distribuicao, string? origem)
    {
        _nota?.GarantirPendente();
        if (fator < 1) throw new ArgumentException("O fator (peças por unidade da nota) precisa ser 1 ou mais.", nameof(fator));
        var pecas = Quantidade * fator;
        if (pecas % 1 != 0)
            throw new ArgumentException($"A nota traz {Quantidade:0.####} {Unidade}: escolha um fator que dê um número inteiro de peças.", nameof(fator));
        if (distribuicao.Count == 0 || distribuicao.Any(d => d.Quantidade <= 0))
            throw new ArgumentException("Informe as quantidades (maiores que zero) de cada SKU.", nameof(distribuicao));
        if (distribuicao.GroupBy(d => d.SkuId).Any(g => g.Count() > 1))
            throw new ArgumentException("O mesmo SKU aparece mais de uma vez.", nameof(distribuicao));
        var soma = distribuicao.Sum(d => d.Quantidade);
        if (soma != pecas)
            throw new ArgumentException($"A distribuição soma {soma} peça(s), mas a nota traz {pecas:0} ({Quantidade:0.####} {Unidade} × {fator}).", nameof(distribuicao));

        (ProdutoId, Fator, Ignorado, OrigemDoVinculo) = (produtoId, fator, false, origem);
        _distribuicao.Clear();
        _distribuicao.AddRange(distribuicao.Select(d => DistribuicaoDoItem.De(Id, d.SkuId, d.Quantidade)));
        return [.. _distribuicao];
    }

    /// <summary>Sugestão sem distribuição (ex.: de-para que lembra só o produto). O item continua pendente.</summary>
    public void Sugerir(Guid produtoId, int fator, string origem) => (ProdutoId, Fator, OrigemDoVinculo) = (produtoId, Math.Max(fator, 1), origem);

    public void Ignorar(bool ignorar)
    {
        _nota?.GarantirPendente();
        Ignorado = ignorar;
    }

    internal void Vincular(NotaDeEntrada nota) => _nota = nota;
}

[DisplayName("Distribuição do item da nota"), NaoAuditar]
public sealed class DistribuicaoDoItem : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid SkuId { get; private set; }
    public int Quantidade { get; private set; }

    private DistribuicaoDoItem() { }

    internal static DistribuicaoDoItem De(Guid itemId, Guid skuId, int quantidade) =>
        new() { Id = Guid.NewGuid(), ItemId = itemId, SkuId = skuId, Quantidade = quantidade };
}

/// <summary>Parcela a pagar informada na nota (usada pelo financeiro a partir da v0.13.0).</summary>
[DisplayName("Duplicata da nota de entrada"), NaoAuditar]
public sealed class DuplicataDaNota : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotaId { get; private set; }
    public string Numero { get; private set; } = "";
    public DateOnly Vencimento { get; private set; }
    public decimal Valor { get; private set; }

    private DuplicataDaNota() { }

    internal static DuplicataDaNota De(Guid notaId, DuplicataDaNfe d) =>
        new() { Id = Guid.NewGuid(), NotaId = notaId, Numero = d.Numero, Vencimento = d.Vencimento, Valor = d.Valor };
}

/// <summary>
/// De-para memorizado: o código do produto no fornecedor (pelo CNPJ) vira o SKU da loja — ou só o produto, quando o
/// fornecedor manda um código por modelo e a grade é distribuída a cada nota.
/// </summary>
[DisplayName("Vínculo de produto do fornecedor")]
public sealed class VinculoDeProduto : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string FornecedorDocumento { get; private set; } = "";
    public string Codigo { get; private set; } = "";
    public Guid ProdutoId { get; private set; }
    public Guid? SkuId { get; private set; }
    public int Fator { get; private set; } = 1;
    public DateTimeOffset AtualizadoEm { get; private set; }

    private VinculoDeProduto() { }

    public static VinculoDeProduto Criar(string fornecedorDocumento, string codigo, Guid produtoId, Guid? skuId, int fator, DateTimeOffset agora) =>
        new() { Id = Guid.NewGuid(), FornecedorDocumento = fornecedorDocumento, Codigo = codigo, ProdutoId = produtoId, SkuId = skuId, Fator = fator, AtualizadoEm = agora };

    public void Atualizar(Guid produtoId, Guid? skuId, int fator, DateTimeOffset agora) => (ProdutoId, SkuId, Fator, AtualizadoEm) = (produtoId, skuId, fator, agora);
}
