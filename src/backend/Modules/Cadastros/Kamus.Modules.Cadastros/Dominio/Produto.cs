using System.ComponentModel;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Cadastros.Dominio;

public sealed record DadosDoProduto
{
    public string? Referencia { get; init; }
    public string? Descricao { get; init; }
    public string? Marca { get; init; }
    public string? Categoria { get; init; }
    public string? Colecao { get; init; }
    public string? ReferenciaDoFornecedor { get; init; }
    public Guid? FornecedorId { get; init; }
    public decimal PrecoVenda { get; init; }
    public decimal? PrecoCusto { get; init; }
}

/// <summary>
/// Produto (modelo), ex.: "Camiseta Básica Gola V". Cada combinação cor × tamanho é um <see cref="Sku"/>, com código de
/// barras e (futuramente) estoque próprios. A grade não muda depois de criado; cores podem entrar e sair.
/// </summary>
[DisplayName("Produto")]
public sealed class Produto : ITenantOwned
{
    private readonly List<Sku> _skus = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Código interno do modelo (único por cliente do SaaS). Não muda depois de criado.</summary>
    public string Referencia { get; private set; } = "";

    public string Descricao { get; private set; } = "";
    public string? Marca { get; private set; }
    public string? Categoria { get; private set; }
    public string? Colecao { get; private set; }
    public string? ReferenciaDoFornecedor { get; private set; }
    public Guid? FornecedorId { get; private set; }
    public Guid GradeId { get; private set; }
    public decimal PrecoVenda { get; private set; }
    public decimal? PrecoCusto { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    public IReadOnlyCollection<Sku> Skus => _skus;

    /// <summary>Preço ÷ custo (2,0 a 2,5 é comum em moda).</summary>
    public decimal? Markup => PrecoCusto is > 0 ? Math.Round(PrecoVenda / PrecoCusto.Value, 2) : null;

    private Produto() { }

    public static IReadOnlyList<string> Validar(DadosDoProduto d)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Referencia)) erros.Add("Informe a referência.");
        else if (d.Referencia.Trim().Length > 30) erros.Add("A referência deve ter até 30 caracteres.");
        if (string.IsNullOrWhiteSpace(d.Descricao)) erros.Add("Informe a descrição.");
        if (d.PrecoVenda < 0) erros.Add("O preço de venda não pode ser negativo.");
        if (d.PrecoCusto < 0) erros.Add("O custo não pode ser negativo.");
        return erros;
    }

    /// <param name="tamanhos">Tamanhos da grade, na ordem de exibição.</param>
    /// <param name="gerarCodigo">Próximo código de barras interno (EAN-13 com prefixo 2).</param>
    public static Produto Criar(DadosDoProduto dados, Guid gradeId, IReadOnlyList<string> tamanhos, IReadOnlyList<Guid> corIds,
        Func<string> gerarCodigo, DateTimeOffset agora)
    {
        if (corIds.Count == 0) throw new ArgumentException("Escolha pelo menos uma cor.", nameof(corIds));
        var produto = new Produto { Id = Guid.NewGuid(), GradeId = gradeId, Referencia = dados.Referencia?.Trim().ToUpperInvariant() ?? "", Ativo = true, CriadoEm = agora };
        produto.Aplicar(dados);
        produto.DefinirCores(corIds, tamanhos, gerarCodigo);
        return produto;
    }

    public void Atualizar(DadosDoProduto dados) => Aplicar(dados with { Referencia = Referencia });

    private void Aplicar(DadosDoProduto d)
    {
        var erros = Validar(d);
        if (erros.Count > 0) throw new ArgumentException(string.Join(" ", erros), nameof(d));
        Descricao = d.Descricao!.Trim();
        Marca = Limpo(d.Marca);
        Categoria = Limpo(d.Categoria);
        Colecao = Limpo(d.Colecao);
        ReferenciaDoFornecedor = Limpo(d.ReferenciaDoFornecedor);
        FornecedorId = d.FornecedorId;
        PrecoVenda = d.PrecoVenda;
        PrecoCusto = d.PrecoCusto;
    }

    /// <summary>Cores novas ganham SKUs; cores retiradas têm os SKUs desativados (o histórico e o estoque continuam).</summary>
    public void DefinirCores(IReadOnlyList<Guid> corIds, IReadOnlyList<string> tamanhos, Func<string> gerarCodigo)
    {
        if (corIds.Count == 0) throw new ArgumentException("Escolha pelo menos uma cor.", nameof(corIds));
        foreach (var sku in _skus.Where(s => !corIds.Contains(s.CorId))) sku.Desativar();

        foreach (var corId in corIds.Distinct())
        {
            var daCor = _skus.Where(s => s.CorId == corId).ToList();
            if (daCor.Count > 0)
            {
                if (daCor.All(s => !s.Ativo)) daCor.ForEach(s => s.Ativar());
                continue;
            }
            for (var i = 0; i < tamanhos.Count; i++)
                _skus.Add(Sku.Criar(Id, corId, tamanhos[i], i, gerarCodigo()));
        }
    }

    public void AlterarSku(Guid skuId, string codigoDeBarras, decimal? precoVenda, bool ativo)
    {
        var sku = _skus.SingleOrDefault(s => s.Id == skuId) ?? throw new ArgumentException("SKU não encontrado.", nameof(skuId));
        sku.Alterar(codigoDeBarras, precoVenda, ativo);
    }

    public decimal PrecoDe(Sku sku) => sku.PrecoVenda ?? PrecoVenda;

    public void Inativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>Variação vendável: produto + cor + tamanho, com código de barras próprio.</summary>
[DisplayName("SKU")]
public sealed class Sku : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Guid CorId { get; private set; }
    public string Tamanho { get; private set; } = "";

    /// <summary>Posição do tamanho na grade (P antes de M, não alfabético).</summary>
    public int OrdemTamanho { get; private set; }

    public string CodigoDeBarras { get; private set; } = "";

    /// <summary>Preço específico deste SKU (ex.: tamanhos plus); nulo = preço do produto.</summary>
    public decimal? PrecoVenda { get; private set; }

    public bool Ativo { get; private set; }

    private Sku() { }

    internal static Sku Criar(Guid produtoId, Guid corId, string tamanho, int ordem, string codigoDeBarras) =>
        new() { Id = Guid.NewGuid(), ProdutoId = produtoId, CorId = corId, Tamanho = tamanho, OrdemTamanho = ordem, CodigoDeBarras = codigoDeBarras, Ativo = true };

    internal void Alterar(string codigoDeBarras, decimal? precoVenda, bool ativo)
    {
        if (!Kamus.SharedKernel.Documentos.CodigoDeBarras.Valido(codigoDeBarras))
            throw new ArgumentException("Código de barras inválido (confira o dígito verificador).", nameof(codigoDeBarras));
        ArgumentOutOfRangeException.ThrowIfNegative(precoVenda ?? 0);
        CodigoDeBarras = Kamus.SharedKernel.Documentos.CodigoDeBarras.Normalizar(codigoDeBarras)!;
        PrecoVenda = precoVenda;
        Ativo = ativo;
    }

    internal void Ativar() => Ativo = true;

    internal void Desativar() => Ativo = false;
}
