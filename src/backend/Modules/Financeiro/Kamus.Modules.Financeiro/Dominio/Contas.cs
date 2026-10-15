using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Financeiro.Dominio;

public enum TipoDeConta
{
    Caixa,
    Banco,
    Carteira,
}

/// <summary>Onde o dinheiro está: caixa da loja, conta bancária ou carteira digital. Toda baixa movimenta uma conta.</summary>
[DisplayName("Conta financeira")]
public sealed class ContaFinanceira : ITenantOwned
{
    public const int TamanhoMaximoDoNome = 60;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = "";
    public TipoDeConta Tipo { get; private set; }

    /// <summary>Saldo no dia em que a conta começou a ser usada no sistema.</summary>
    public decimal SaldoInicial { get; private set; }

    public DateOnly SaldoInicialEm { get; private set; }
    public bool Ativa { get; private set; }

    private ContaFinanceira() { }

    public static ContaFinanceira Criar(Guid empresaId, string nome, TipoDeConta tipo, decimal saldoInicial, DateOnly desde)
    {
        var conta = new ContaFinanceira { Id = Guid.NewGuid(), EmpresaId = empresaId, Ativa = true };
        conta.Atualizar(nome, tipo, saldoInicial, desde);
        return conta;
    }

    public void Atualizar(string nome, TipoDeConta tipo, decimal saldoInicial, DateOnly desde)
    {
        var aparado = nome?.Trim() ?? "";
        if (aparado.Length == 0) throw new ArgumentException("Informe o nome da conta.", nameof(nome));
        if (aparado.Length > TamanhoMaximoDoNome) throw new ArgumentException($"O nome tem no máximo {TamanhoMaximoDoNome} caracteres.", nameof(nome));
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("Tipo de conta inválido.", nameof(tipo));
        (Nome, Tipo, SaldoInicial, SaldoInicialEm) = (aparado, tipo, Math.Round(saldoInicial, 2), desde);
    }

    public void Desativar() => Ativa = false;

    public void Reativar() => Ativa = true;
}

public enum TipoDeCategoria
{
    Receita,
    Despesa,
}

/// <summary>Plano de contas simples (grupo › categoria), base do fluxo de caixa por categoria e do DRE. Vale para todas as empresas do cliente.</summary>
[DisplayName("Categoria financeira")]
public sealed class Categoria : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = "";
    public string Grupo { get; private set; } = "";
    public TipoDeCategoria Tipo { get; private set; }
    public bool Ativa { get; private set; }

    public static readonly IReadOnlyList<(string Grupo, string Nome, TipoDeCategoria Tipo)> Padroes =
    [
        ("Vendas", "Vendas", TipoDeCategoria.Receita),
        ("Outras receitas", "Outras receitas", TipoDeCategoria.Receita),
        ("Outras receitas", "Rendimentos financeiros", TipoDeCategoria.Receita),
        ("Mercadorias", "Compra de mercadorias", TipoDeCategoria.Despesa),
        ("Mercadorias", "Frete de compras", TipoDeCategoria.Despesa),
        ("Ocupação", "Aluguel", TipoDeCategoria.Despesa),
        ("Ocupação", "Condomínio e IPTU", TipoDeCategoria.Despesa),
        ("Ocupação", "Energia elétrica", TipoDeCategoria.Despesa),
        ("Ocupação", "Água", TipoDeCategoria.Despesa),
        ("Ocupação", "Internet e telefone", TipoDeCategoria.Despesa),
        ("Pessoal", "Salários", TipoDeCategoria.Despesa),
        ("Pessoal", "Comissões", TipoDeCategoria.Despesa),
        ("Pessoal", "Encargos e benefícios", TipoDeCategoria.Despesa),
        ("Impostos", "Simples Nacional", TipoDeCategoria.Despesa),
        ("Impostos", "Outros impostos e taxas", TipoDeCategoria.Despesa),
        ("Administrativas", "Contabilidade", TipoDeCategoria.Despesa),
        ("Administrativas", "Sistemas e assinaturas", TipoDeCategoria.Despesa),
        ("Administrativas", "Material de escritório e embalagens", TipoDeCategoria.Despesa),
        ("Financeiras", "Tarifas bancárias", TipoDeCategoria.Despesa),
        ("Financeiras", "Taxas de cartão", TipoDeCategoria.Despesa),
        ("Financeiras", "Juros e multas pagos", TipoDeCategoria.Despesa),
        ("Marketing", "Marketing e divulgação", TipoDeCategoria.Despesa),
        ("Outras despesas", "Outras despesas", TipoDeCategoria.Despesa),
    ];

    private Categoria() { }

    public static Categoria Criar(string nome, string grupo, TipoDeCategoria tipo)
    {
        var categoria = new Categoria { Id = Guid.NewGuid(), Ativa = true };
        categoria.Atualizar(nome, grupo, tipo);
        return categoria;
    }

    public void Atualizar(string nome, string grupo, TipoDeCategoria tipo)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome da categoria.", nameof(nome));
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("Tipo de categoria inválido.", nameof(tipo));
        (Nome, Grupo, Tipo) = (nome.Trim()[..Math.Min(nome.Trim().Length, 60)], string.IsNullOrWhiteSpace(grupo) ? nome.Trim() : grupo.Trim()[..Math.Min(grupo.Trim().Length, 60)], tipo);
    }

    public void Desativar() => Ativa = false;

    public void Reativar() => Ativa = true;
}
