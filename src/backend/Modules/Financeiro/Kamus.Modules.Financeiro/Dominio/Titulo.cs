using System.ComponentModel;
using System.Globalization;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Financeiro.Dominio;

public enum TipoDeTitulo
{
    Pagar,
    Receber,
}

public enum SituacaoDaParcela
{
    Aberta,
    Vencida,
    Parcial,
    Paga,
    Cancelada,
}

/// <param name="Competencia">Quando o fato aconteceu (base do DRE). O vencimento das parcelas é a base do fluxo de caixa.</param>
public sealed record DadosDoTitulo(TipoDeTitulo Tipo, string Descricao, Guid? PessoaId, string? PessoaNome, Guid? CategoriaId, string? Documento,
    DateOnly Competencia, string? Observacao);

public sealed record NovaParcela(DateOnly Vencimento, decimal Valor);

/// <summary>Conta a pagar ou a receber, dividida em parcelas. Cada parcela recebe baixas (podem ser parciais) que movimentam uma conta financeira.</summary>
[DisplayName("Título financeiro")]
public sealed class Titulo : ITenantOwned
{
    public const string OrigemManual = "Manual";
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly List<Parcela> _parcelas = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public TipoDeTitulo Tipo { get; private set; }
    public string Descricao { get; private set; } = "";
    public Guid? PessoaId { get; private set; }
    public string? PessoaNome { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public string? Documento { get; private set; }
    public DateOnly Competencia { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>De onde veio (Manual, NotaDeEntrada, Venda...). Com <see cref="OrigemId"/>, impede gerar o mesmo título duas vezes.</summary>
    public string Origem { get; private set; } = OrigemManual;

    public string? OrigemId { get; private set; }
    public bool Cancelado { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    public IReadOnlyCollection<Parcela> Parcelas => _parcelas;

    public decimal ValorTotal => _parcelas.Sum(p => p.Valor);

    public bool Quitado => !Cancelado && _parcelas.All(p => p.Saldo == 0);

    private Titulo() { }

    internal static string Moeda(decimal valor) => valor.ToString("N2", PtBr);

    public static Titulo Criar(Guid empresaId, DadosDoTitulo dados, IReadOnlyList<NovaParcela> parcelas, DateTimeOffset agora,
        string origem = OrigemManual, string? origemId = null)
    {
        if (parcelas.Count == 0) throw new ArgumentException("Informe ao menos uma parcela.", nameof(parcelas));
        if (parcelas.Count > 120) throw new ArgumentException("No máximo 120 parcelas.", nameof(parcelas));
        if (parcelas.Any(p => p.Valor <= 0)) throw new ArgumentException("O valor de cada parcela precisa ser maior que zero.", nameof(parcelas));
        if (parcelas.Any(p => decimal.Round(p.Valor, 2) != p.Valor)) throw new ArgumentException("Os valores das parcelas vão até os centavos.", nameof(parcelas));
        if (!Enum.IsDefined(dados.Tipo)) throw new ArgumentException("Tipo inválido: pagar ou receber.", nameof(dados));

        var titulo = new Titulo { Id = Guid.NewGuid(), EmpresaId = empresaId, Tipo = dados.Tipo, CriadoEm = agora, Origem = origem, OrigemId = origemId };
        titulo.Atualizar(dados);
        var numero = 1;
        foreach (var p in parcelas.OrderBy(p => p.Vencimento)) titulo._parcelas.Add(Parcela.Criar(titulo, numero++, p.Vencimento, p.Valor));
        return titulo;
    }

    /// <summary>Divide o total em parcelas mensais (o mesmo dia do mês, ou o último dia quando o mês é mais curto); os centavos que sobram vão na primeira.</summary>
    public static IReadOnlyList<NovaParcela> GerarParcelas(decimal total, int quantidade, DateOnly primeiroVencimento)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantidade, 1);
        var valor = Math.Floor(total / quantidade * 100) / 100;
        var resto = total - valor * quantidade;
        return Enumerable.Range(0, quantidade).Select(i => new NovaParcela(primeiroVencimento.AddMonths(i), i == 0 ? valor + resto : valor)).ToList();
    }

    /// <summary>Altera os dados do título (o tipo pagar/receber não muda).</summary>
    public void Atualizar(DadosDoTitulo dados)
    {
        if (string.IsNullOrWhiteSpace(dados.Descricao)) throw new ArgumentException("Informe a descrição.", nameof(dados));
        Descricao = Limitar(dados.Descricao, 150)!;
        PessoaId = dados.PessoaId;
        PessoaNome = Limitar(dados.PessoaNome, 150);
        CategoriaId = dados.CategoriaId;
        Documento = Limitar(dados.Documento, 60);
        Competencia = dados.Competencia;
        Observacao = Limitar(dados.Observacao, 1000);
    }

    /// <summary>Religa as parcelas ao título depois de carregar do banco (para as regras que dependem do cancelamento).</summary>
    internal void Reconectar()
    {
        foreach (var p in _parcelas) p.Vincular(this);
    }

    public void Cancelar(string? usuario, DateTimeOffset agora)
    {
        if (Cancelado) throw new InvalidOperationException("Este título já está cancelado.");
        if (_parcelas.Any(p => p.Baixas.Any(b => !b.Estornada)))
            throw new InvalidOperationException("O título tem baixas: estorne as baixas antes de cancelar.");
        Cancelado = true;
        Observacao = Limitar($"{Observacao}{(Observacao is null ? "" : " · ")}Cancelado por {usuario ?? "?"} em {agora:dd/MM/yyyy}.", 1000);
    }

    private static string? Limitar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, maximo)];
}

[DisplayName("Parcela")]
public sealed class Parcela : ITenantOwned
{
    private readonly List<Baixa> _baixas = [];
    private Titulo? _titulo;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TituloId { get; private set; }
    public int Numero { get; private set; }
    public DateOnly Vencimento { get; private set; }
    public decimal Valor { get; private set; }

    public IReadOnlyCollection<Baixa> Baixas => _baixas;

    /// <summary>Principal já abatido (sem juros/multa/desconto), contando só baixas não estornadas.</summary>
    public decimal ValorPago => _baixas.Where(b => !b.Estornada).Sum(b => b.Valor);

    public decimal Saldo => Valor - ValorPago;

    private Parcela() { }

    internal static Parcela Criar(Titulo titulo, int numero, DateOnly vencimento, decimal valor) =>
        new() { Id = Guid.NewGuid(), TituloId = titulo.Id, _titulo = titulo, Numero = numero, Vencimento = vencimento, Valor = valor };

    internal void Vincular(Titulo titulo) => _titulo = titulo;

    public SituacaoDaParcela Situacao(DateOnly hoje) =>
        _titulo?.Cancelado == true ? SituacaoDaParcela.Cancelada
        : Saldo == 0 ? SituacaoDaParcela.Paga
        : Vencimento < hoje ? SituacaoDaParcela.Vencida
        : ValorPago > 0 ? SituacaoDaParcela.Parcial
        : SituacaoDaParcela.Aberta;

    /// <param name="valor">Quanto do principal está sendo pago (até o saldo).</param>
    public Baixa Baixar(DateOnly data, Guid contaId, decimal valor, decimal juros, decimal multa, decimal desconto, string? usuario, DateTimeOffset agora)
    {
        if (_titulo?.Cancelado == true) throw new InvalidOperationException("Título cancelado não recebe baixa.");
        if (valor <= 0) throw new ArgumentException("Informe o valor da baixa.", nameof(valor));
        if (valor > Saldo) throw new ArgumentException($"O valor passa do saldo da parcela (R$ {Titulo.Moeda(Saldo)}).", nameof(valor));
        if (juros < 0 || multa < 0 || desconto < 0) throw new ArgumentException("Juros, multa e desconto não podem ser negativos.", nameof(juros));
        if (desconto >= valor + juros + multa) throw new ArgumentException("O desconto precisa ser menor que o valor pago.", nameof(desconto));
        var baixa = Baixa.Criar(Id, data, contaId, decimal.Round(valor, 2), decimal.Round(juros, 2), decimal.Round(multa, 2), decimal.Round(desconto, 2), usuario, agora);
        _baixas.Add(baixa);
        return baixa;
    }

    public Baixa Estornar(Guid baixaId, string? usuario, DateTimeOffset agora)
    {
        var baixa = _baixas.FirstOrDefault(b => b.Id == baixaId) ?? throw new InvalidOperationException("Baixa não encontrada.");
        if (baixa.Estornada) throw new InvalidOperationException("Esta baixa já foi estornada.");
        baixa.Estornar(usuario, agora);
        return baixa;
    }
}

/// <summary>Pagamento ou recebimento (total ou parcial) de uma parcela.</summary>
[DisplayName("Baixa")]
public sealed class Baixa : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ParcelaId { get; private set; }
    public DateOnly Data { get; private set; }
    public Guid ContaId { get; private set; }

    /// <summary>Principal abatido da parcela.</summary>
    public decimal Valor { get; private set; }

    public decimal Juros { get; private set; }
    public decimal Multa { get; private set; }
    public decimal Desconto { get; private set; }
    public string? Usuario { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }
    public bool Estornada { get; private set; }
    public string? EstornadaPor { get; private set; }
    public DateTimeOffset? EstornadaEm { get; private set; }

    /// <summary>O que de fato entrou/saiu da conta: principal + juros + multa − desconto.</summary>
    public decimal Total => Valor + Juros + Multa - Desconto;

    private Baixa() { }

    internal static Baixa Criar(Guid parcelaId, DateOnly data, Guid contaId, decimal valor, decimal juros, decimal multa, decimal desconto, string? usuario, DateTimeOffset agora) =>
        new()
        {
            Id = Guid.NewGuid(),
            ParcelaId = parcelaId,
            Data = data,
            ContaId = contaId,
            Valor = valor,
            Juros = juros,
            Multa = multa,
            Desconto = desconto,
            Usuario = usuario,
            RegistradaEm = agora,
        };

    internal void Estornar(string? usuario, DateTimeOffset agora) => (Estornada, EstornadaPor, EstornadaEm) = (true, usuario, agora);
}

public enum OrigemDoMovimento
{
    Baixa,
    Estorno,
    Transferencia,
}

/// <summary>Linha do extrato de uma conta financeira (imutável). Estorno gera um movimento contrário, não apaga o original.</summary>
[DisplayName("Movimento financeiro"), NaoAuditar]
public sealed class MovimentoFinanceiro : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContaId { get; private set; }
    public DateOnly Data { get; private set; }

    /// <summary>Positivo = entrada; negativo = saída.</summary>
    public decimal Valor { get; private set; }

    public string Descricao { get; private set; } = "";
    public OrigemDoMovimento Origem { get; private set; }
    public Guid? BaixaId { get; private set; }
    public Guid? TituloId { get; private set; }
    public Guid? CategoriaId { get; private set; }

    /// <summary>Liga a saída e a entrada da mesma transferência.</summary>
    public Guid? TransferenciaId { get; private set; }

    public string? Usuario { get; private set; }
    public DateTimeOffset RegistradoEm { get; private set; }

    private MovimentoFinanceiro() { }

    private static decimal Sinal(Titulo t) => t.Tipo == TipoDeTitulo.Receber ? 1 : -1;

    private static string Nome(Titulo t, Parcela p) => $"{t.Descricao} ({p.Numero}/{t.Parcelas.Count})";

    public static MovimentoFinanceiro DaBaixa(Titulo titulo, Parcela parcela, Baixa baixa, DateTimeOffset agora) => new()
    {
        Id = Guid.NewGuid(),
        ContaId = baixa.ContaId,
        Data = baixa.Data,
        Valor = Sinal(titulo) * baixa.Total,
        Descricao = Nome(titulo, parcela),
        Origem = OrigemDoMovimento.Baixa,
        BaixaId = baixa.Id,
        TituloId = titulo.Id,
        CategoriaId = titulo.CategoriaId,
        Usuario = baixa.Usuario,
        RegistradoEm = agora,
    };

    public static MovimentoFinanceiro DoEstorno(Titulo titulo, Parcela parcela, Baixa baixa, DateOnly data, DateTimeOffset agora) => new()
    {
        Id = Guid.NewGuid(),
        ContaId = baixa.ContaId,
        Data = data,
        Valor = -Sinal(titulo) * baixa.Total,
        Descricao = $"Estorno: {Nome(titulo, parcela)}",
        Origem = OrigemDoMovimento.Estorno,
        BaixaId = baixa.Id,
        TituloId = titulo.Id,
        CategoriaId = titulo.CategoriaId,
        Usuario = baixa.EstornadaPor,
        RegistradoEm = agora,
    };

    public static (MovimentoFinanceiro Saida, MovimentoFinanceiro Entrada) Transferencia(ContaFinanceira origem, ContaFinanceira destino, DateOnly data,
        decimal valor, string? observacao, string? usuario, DateTimeOffset agora)
    {
        if (origem.Id == destino.Id) throw new ArgumentException("Escolha contas diferentes.", nameof(destino));
        if (valor <= 0) throw new ArgumentException("Informe o valor da transferência.", nameof(valor));
        var id = Guid.NewGuid();
        var sufixo = string.IsNullOrWhiteSpace(observacao) ? "" : $" — {observacao.Trim()}";
        MovimentoFinanceiro Novo(Guid conta, decimal v, string texto) => new()
        {
            Id = Guid.NewGuid(),
            ContaId = conta,
            Data = data,
            Valor = v,
            Descricao = (texto + sufixo)[..Math.Min((texto + sufixo).Length, 200)],
            Origem = OrigemDoMovimento.Transferencia,
            TransferenciaId = id,
            Usuario = usuario,
            RegistradoEm = agora,
        };
        var v2 = decimal.Round(valor, 2);
        return (Novo(origem.Id, -v2, $"Transferência para {destino.Nome}"), Novo(destino.Id, v2, $"Transferência de {origem.Nome}"));
    }
}
