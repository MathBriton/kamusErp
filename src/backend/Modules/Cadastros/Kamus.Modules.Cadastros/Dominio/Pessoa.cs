using System.ComponentModel;
using System.Net.Mail;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Cadastros.Dominio;

public enum TipoPessoa
{
    Fisica,
    Juridica,
}

public sealed record Endereco
{
    public string? Cep { get; init; }
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Complemento { get; init; }
    public string? Bairro { get; init; }
    public string? Cidade { get; init; }
    public string? Uf { get; init; }
}

/// <summary>Tamanhos e estilo preferidos — usados no atendimento ("chegou novidade no seu tamanho").</summary>
public sealed record PreferenciasDeModa
{
    public string? Blusa { get; init; }
    public string? Calca { get; init; }
    public string? Calcado { get; init; }
    public string? Estilo { get; init; }
}

/// <summary>Dados editáveis de uma pessoa (o que vem do formulário).</summary>
public sealed record DadosDaPessoa
{
    public TipoPessoa Tipo { get; init; }
    public string? Nome { get; init; }
    public string? NomeFantasia { get; init; }
    public string? Documento { get; init; }
    public bool EhCliente { get; init; }
    public bool EhFornecedor { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public DateOnly? DataNascimento { get; init; }
    public Endereco? Endereco { get; init; }
    public PreferenciasDeModa? Preferencias { get; init; }
    public string? Observacoes { get; init; }
    public bool AceitaMarketing { get; init; }
}

/// <summary>
/// Cliente e/ou fornecedor (uma pessoa pode ter os dois papéis). Pertence ao cliente do SaaS (tenant), não a um CNPJ:
/// a mesma ficha vale em todas as lojas.
/// </summary>
[DisplayName("Pessoa")]
public sealed class Pessoa : ITenantOwned
{
    public const string NomeAnonimizado = "Cliente anonimizado";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public TipoPessoa Tipo { get; private set; }
    public string Nome { get; private set; } = "";
    public string? NomeFantasia { get; private set; }

    /// <summary>CPF ou CNPJ sem máscara (opcional).</summary>
    public string? Documento { get; private set; }

    public bool EhCliente { get; private set; }
    public bool EhFornecedor { get; private set; }
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public DateOnly? DataNascimento { get; private set; }
    // Endereço e preferências ficam em colunas da própria pessoa (o histórico registra cada campo).
    public string? EnderecoCep { get; private set; }
    public string? EnderecoLogradouro { get; private set; }
    public string? EnderecoNumero { get; private set; }
    public string? EnderecoComplemento { get; private set; }
    public string? EnderecoBairro { get; private set; }
    public string? EnderecoCidade { get; private set; }
    public string? EnderecoUf { get; private set; }
    public string? PreferenciaBlusa { get; private set; }
    public string? PreferenciaCalca { get; private set; }
    public string? PreferenciaCalcado { get; private set; }
    public string? PreferenciaEstilo { get; private set; }

    public Endereco Endereco => new()
    {
        Cep = EnderecoCep,
        Logradouro = EnderecoLogradouro,
        Numero = EnderecoNumero,
        Complemento = EnderecoComplemento,
        Bairro = EnderecoBairro,
        Cidade = EnderecoCidade,
        Uf = EnderecoUf,
    };

    public PreferenciasDeModa Preferencias => new() { Blusa = PreferenciaBlusa, Calca = PreferenciaCalca, Calcado = PreferenciaCalcado, Estilo = PreferenciaEstilo };
    public string? Observacoes { get; private set; }

    /// <summary>LGPD: data em que o cliente aceitou receber mensagens de marketing (opt-in). Nulo = não aceita.</summary>
    public DateTimeOffset? ConsentimentoMarketingEm { get; private set; }

    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? AnonimizadoEm { get; private set; }

    public string? WhatsApp => Dominio.Telefone.ParaWhatsApp(Telefone);

    private Pessoa() { }

    /// <returns>Mensagens prontas para exibir; vazia quando os dados são válidos.</returns>
    public static IReadOnlyList<string> Validar(DadosDaPessoa d, DateOnly hoje)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Nome)) erros.Add("Informe o nome.");
        if (!d.EhCliente && !d.EhFornecedor) erros.Add("Marque se é cliente ou fornecedor.");
        if (!string.IsNullOrWhiteSpace(d.Documento) && NormalizarDocumento(d.Tipo, d.Documento) is null)
            erros.Add(d.Tipo == TipoPessoa.Fisica ? "CPF inválido." : "CNPJ inválido.");
        if (!string.IsNullOrWhiteSpace(d.Telefone) && Dominio.Telefone.Normalizar(d.Telefone) is null)
            erros.Add("Telefone inválido: informe com DDD.");
        if (!string.IsNullOrWhiteSpace(d.Email) && !MailAddress.TryCreate(d.Email.Trim(), out _))
            erros.Add("E-mail inválido.");
        if (d.DataNascimento is { } nascimento && (nascimento > hoje || nascimento.Year < 1900))
            erros.Add("Data de nascimento inválida.");
        if (d.Endereco?.Uf is { Length: > 0 } uf && (uf.Trim().Length != 2 || !uf.Trim().All(char.IsAsciiLetter)))
            erros.Add("UF deve ter 2 letras.");
        return erros;
    }

    public static Pessoa Criar(DadosDaPessoa dados, DateTimeOffset agora, DateOnly hoje)
    {
        var pessoa = new Pessoa { Id = Guid.NewGuid(), Ativo = true, CriadoEm = agora };
        pessoa.Aplicar(dados, agora, hoje);
        return pessoa;
    }

    public void Atualizar(DadosDaPessoa dados, DateTimeOffset agora, DateOnly hoje)
    {
        if (AnonimizadoEm is not null) throw new InvalidOperationException("Cadastro anonimizado não pode ser alterado.");
        Aplicar(dados, agora, hoje);
    }

    private void Aplicar(DadosDaPessoa d, DateTimeOffset agora, DateOnly hoje)
    {
        var erros = Validar(d, hoje);
        if (erros.Count > 0) throw new ArgumentException(string.Join(" ", erros), nameof(d));

        Tipo = d.Tipo;
        Nome = d.Nome!.Trim();
        NomeFantasia = Limpo(d.NomeFantasia);
        Documento = string.IsNullOrWhiteSpace(d.Documento) ? null : NormalizarDocumento(d.Tipo, d.Documento);
        EhCliente = d.EhCliente;
        EhFornecedor = d.EhFornecedor;
        Telefone = Dominio.Telefone.Normalizar(d.Telefone);
        Email = Limpo(d.Email)?.ToLowerInvariant();
        DataNascimento = d.DataNascimento;
        var e = d.Endereco ?? new Endereco();
        (EnderecoCep, EnderecoLogradouro, EnderecoNumero, EnderecoComplemento, EnderecoBairro, EnderecoCidade, EnderecoUf) =
            (Limpo(e.Cep), Limpo(e.Logradouro), Limpo(e.Numero), Limpo(e.Complemento), Limpo(e.Bairro), Limpo(e.Cidade), Limpo(e.Uf)?.ToUpperInvariant());
        var p = d.Preferencias ?? new PreferenciasDeModa();
        (PreferenciaBlusa, PreferenciaCalca, PreferenciaCalcado, PreferenciaEstilo) = (Limpo(p.Blusa), Limpo(p.Calca), Limpo(p.Calcado), Limpo(p.Estilo));
        Observacoes = Limpo(d.Observacoes);
        ConsentimentoMarketingEm = d.AceitaMarketing ? ConsentimentoMarketingEm ?? agora : null;
    }

    public void Inativar() => Ativo = false;

    public void Reativar()
    {
        if (AnonimizadoEm is not null) throw new InvalidOperationException("Cadastro anonimizado não pode ser reativado.");
        Ativo = true;
    }

    /// <summary>LGPD: apaga os dados pessoais e mantém o registro (o histórico de vendas continua apontando para ele).</summary>
    public void Anonimizar(DateTimeOffset agora)
    {
        Nome = NomeAnonimizado;
        NomeFantasia = Documento = Telefone = Email = Observacoes = null;
        DataNascimento = null;
        EnderecoCep = EnderecoLogradouro = EnderecoNumero = EnderecoComplemento = EnderecoBairro = EnderecoCidade = EnderecoUf = null;
        PreferenciaBlusa = PreferenciaCalca = PreferenciaCalcado = PreferenciaEstilo = null;
        ConsentimentoMarketingEm = null;
        Ativo = false;
        AnonimizadoEm = agora;
    }

    /// <summary>Aniversário (dia/mês) cai entre as datas, inclusive — considera a virada do ano.</summary>
    public bool FazAniversarioEntre(DateOnly inicio, DateOnly fim)
    {
        if (DataNascimento is not { } n) return false;
        for (var ano = inicio.Year; ano <= fim.Year; ano++)
        {
            var dia = n.Month == 2 && n.Day == 29 && !DateTime.IsLeapYear(ano) ? new DateOnly(ano, 2, 28) : new DateOnly(ano, n.Month, n.Day);
            if (dia >= inicio && dia <= fim) return true;
        }
        return false;
    }

    public static string? NormalizarDocumento(TipoPessoa tipo, string documento) =>
        tipo == TipoPessoa.Fisica
            ? Cpf.TryParse(documento, out var cpf) ? cpf.Valor : null
            : Cnpj.TryParse(documento, out var cnpj) ? cnpj.Valor : null;

    public string? DocumentoFormatado => Documento is null ? null
        : Tipo == TipoPessoa.Fisica ? Cpf.Parse(Documento).Formatado : Cnpj.Parse(Documento).Formatado;

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
