using Kamus.Modules.Cadastros.Dominio;

namespace Kamus.UnitTests.Cadastros;

public class PessoaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 14);

    private static DadosDaPessoa Cliente(string nome = "Maria Silva") => new()
    {
        Tipo = TipoPessoa.Fisica,
        Nome = nome,
        EhCliente = true,
        Telefone = "(11) 98765-4321",
    };

    [Fact]
    public void Criar_ClienteComDadosMinimos()
    {
        var pessoa = Pessoa.Criar(Cliente(" Maria Silva "), Agora, Hoje);

        pessoa.Nome.ShouldBe("Maria Silva");
        pessoa.EhCliente.ShouldBeTrue();
        pessoa.EhFornecedor.ShouldBeFalse();
        pessoa.Telefone.ShouldBe("11987654321");
        pessoa.Ativo.ShouldBeTrue();
        pessoa.WhatsApp.ShouldBe("5511987654321");
    }

    [Fact]
    public void Validar_SemNomeSemPapel_ApontaOsDois()
    {
        var erros = Pessoa.Validar(new DadosDaPessoa { Tipo = TipoPessoa.Fisica, Nome = " " }, Hoje);

        erros.ShouldContain(e => e.Contains("nome", StringComparison.OrdinalIgnoreCase));
        erros.ShouldContain(e => e.Contains("cliente ou fornecedor", StringComparison.OrdinalIgnoreCase));
        Should.Throw<ArgumentException>(() => Pessoa.Criar(new DadosDaPessoa { Tipo = TipoPessoa.Fisica, Nome = " " }, Agora, Hoje));
    }

    [Theory]
    [InlineData(TipoPessoa.Fisica, "529.982.247-25", "52998224725")]
    [InlineData(TipoPessoa.Juridica, "11.222.333/0001-81", "11222333000181")]
    public void Documento_ConformeOTipo(TipoPessoa tipo, string documento, string esperado)
    {
        var pessoa = Pessoa.Criar(Cliente() with { Tipo = tipo, Documento = documento }, Agora, Hoje);

        pessoa.Documento.ShouldBe(esperado);
    }

    [Theory]
    [InlineData(TipoPessoa.Fisica, "11.222.333/0001-81", "CPF inválido.")]
    [InlineData(TipoPessoa.Juridica, "529.982.247-25", "CNPJ inválido.")]
    public void Documento_TrocadoOuInvalido_Recusa(TipoPessoa tipo, string documento, string erro) =>
        Pessoa.Validar(Cliente() with { Tipo = tipo, Documento = documento }, Hoje).ShouldContain(erro);

    [Theory]
    [InlineData("98765-4321")]
    [InlineData("abc")]
    public void Telefone_SemDdd_Recusa(string telefone) =>
        Pessoa.Validar(Cliente() with { Telefone = telefone }, Hoje).ShouldContain(e => e.Contains("Telefone"));

    [Fact]
    public void Telefone_ComCodigoDoPais_GuardaSemO55()
    {
        var pessoa = Pessoa.Criar(Cliente() with { Telefone = "+55 11 3333-4444" }, Agora, Hoje);

        pessoa.Telefone.ShouldBe("1133334444");
        pessoa.WhatsApp.ShouldBe("551133334444");
    }

    [Fact]
    public void EmailENascimento_Validados()
    {
        var erros = Pessoa.Validar(Cliente() with { Email = "sem-arroba", DataNascimento = Hoje.AddDays(1) }, Hoje);

        erros.ShouldContain(e => e.Contains("E-mail"));
        erros.ShouldContain(e => e.Contains("nascimento"));
    }

    [Fact]
    public void Uf_DuasLetras()
    {
        Pessoa.Validar(Cliente() with { Endereco = new Endereco { Cidade = "São Paulo", Uf = "Sao" } }, Hoje)
            .ShouldContain(e => e.Contains("UF"));
        Pessoa.Criar(Cliente() with { Endereco = new Endereco { Cidade = "São Paulo", Uf = "sp" } }, Agora, Hoje).Endereco.Uf.ShouldBe("SP");
    }

    [Fact]
    public void ConsentimentoDeMarketing_GuardaADataDoOptInEMantemAoEditar()
    {
        var pessoa = Pessoa.Criar(Cliente() with { AceitaMarketing = true }, Agora, Hoje);
        pessoa.ConsentimentoMarketingEm.ShouldBe(Agora);

        pessoa.Atualizar(Cliente() with { AceitaMarketing = true, Nome = "Maria S." }, Agora.AddDays(5), Hoje);
        pessoa.ConsentimentoMarketingEm.ShouldBe(Agora);

        pessoa.Atualizar(Cliente() with { AceitaMarketing = false }, Agora.AddDays(6), Hoje);
        pessoa.ConsentimentoMarketingEm.ShouldBeNull();
    }

    [Fact]
    public void Preferencias_DeModa()
    {
        var pessoa = Pessoa.Criar(Cliente() with { Preferencias = new PreferenciasDeModa { Blusa = "M", Calca = "40", Calcado = "37", Estilo = "Cores neutras" } }, Agora, Hoje);

        pessoa.Preferencias.Calca.ShouldBe("40");
        pessoa.Preferencias.Estilo.ShouldBe("Cores neutras");
    }

    [Fact]
    public void Anonimizar_ApagaDadosPessoaisEInativa()
    {
        var pessoa = Pessoa.Criar(Cliente() with
        {
            Documento = "529.982.247-25",
            Email = "maria@email.com",
            DataNascimento = new DateOnly(1990, 5, 10),
            Endereco = new Endereco { Logradouro = "Rua A", Cidade = "Santos", Uf = "SP" },
            Observacoes = "Gosta de azul",
            AceitaMarketing = true,
        }, Agora, Hoje);

        pessoa.Anonimizar(Agora);

        pessoa.Nome.ShouldBe("Cliente anonimizado");
        pessoa.Documento.ShouldBeNull();
        pessoa.Telefone.ShouldBeNull();
        pessoa.Email.ShouldBeNull();
        pessoa.DataNascimento.ShouldBeNull();
        pessoa.Endereco.Logradouro.ShouldBeNull();
        pessoa.Observacoes.ShouldBeNull();
        pessoa.ConsentimentoMarketingEm.ShouldBeNull();
        pessoa.AnonimizadoEm.ShouldBe(Agora);
        pessoa.Ativo.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => pessoa.Atualizar(Cliente(), Agora, Hoje));
    }

    [Theory]
    [InlineData(10, 14, true)]
    [InlineData(10, 20, true)]
    [InlineData(10, 21, false)]
    [InlineData(10, 13, false)]
    public void FazAniversarioEntre(int mes, int dia, bool esperado)
    {
        var pessoa = Pessoa.Criar(Cliente() with { DataNascimento = new DateOnly(1990, mes, dia) }, Agora, Hoje);

        pessoa.FazAniversarioEntre(Hoje, Hoje.AddDays(6)).ShouldBe(esperado);
    }

    [Fact]
    public void FazAniversarioEntre_ViradaDoAno()
    {
        var pessoa = Pessoa.Criar(Cliente() with { DataNascimento = new DateOnly(1990, 1, 2) }, Agora, Hoje);

        pessoa.FazAniversarioEntre(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3)).ShouldBeTrue();
    }
}
