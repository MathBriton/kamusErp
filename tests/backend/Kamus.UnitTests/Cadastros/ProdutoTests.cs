using Kamus.Modules.Cadastros.Dominio;

namespace Kamus.UnitTests.Cadastros;

public class GradeECorTests
{
    [Fact]
    public void Grade_GuardaTamanhosNaOrdemSemRepetir()
    {
        var grade = GradeDeTamanhos.Criar(" PP ao GG ", ["PP", " P ", "M", "G", "GG"]);

        grade.Nome.ShouldBe("PP ao GG");
        grade.Tamanhos.ShouldBe(["PP", "P", "M", "G", "GG"]);
    }

    [Fact]
    public void Grade_TamanhoRepetidoOuVazio_Recusa()
    {
        Should.Throw<ArgumentException>(() => GradeDeTamanhos.Criar("X", ["P", "p"]));
        Should.Throw<ArgumentException>(() => GradeDeTamanhos.Criar("X", []));
        Should.Throw<ArgumentException>(() => GradeDeTamanhos.Criar("X", ["P", " "]));
    }

    [Fact]
    public void GradesPadrao_IncluemRoupaNumeracaoInfantilEUnico()
    {
        GradeDeTamanhos.Padroes.Select(p => p.Nome).ShouldContain("PP ao GG");
        GradeDeTamanhos.Padroes.ShouldContain(p => p.Tamanhos.Contains("40"));
        GradeDeTamanhos.Padroes.ShouldContain(p => p.Tamanhos.SequenceEqual(new[] { "Único" }));
    }

    [Theory]
    [InlineData("#1D4ED8", "#1D4ED8")]
    [InlineData("#1d4ed8", "#1D4ED8")]
    [InlineData(null, null)]
    public void Cor_HexOpcional(string? hex, string? esperado) => Cor.Criar("Azul", hex).Hex.ShouldBe(esperado);

    [Fact]
    public void Cor_HexInvalido_Recusa() => Should.Throw<ArgumentException>(() => Cor.Criar("Azul", "azul"));
}

public class ProdutoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] Tamanhos = ["P", "M", "G"];
    private static readonly Guid Azul = Guid.NewGuid();
    private static readonly Guid Preto = Guid.NewGuid();
    private static readonly Guid Grade = Guid.NewGuid();

    private static DadosDoProduto Dados() => new()
    {
        Referencia = "CAM-001",
        Descricao = "Camiseta Básica Gola V",
        Marca = "Kamus",
        Categoria = "Camisetas",
        Colecao = "Verão 2027",
        PrecoVenda = 59.90m,
        PrecoCusto = 24m,
    };

    private static Func<string> Gerador()
    {
        var seq = 0L;
        return () => Kamus.SharedKernel.Documentos.CodigoDeBarras.Interno(++seq);
    }

    private static Produto Camiseta() => Produto.Criar(Dados(), Grade, Tamanhos, [Azul, Preto], Gerador(), Agora);

    [Fact]
    public void Criar_GeraUmSkuPorCorETamanhoComCodigoInterno()
    {
        var produto = Camiseta();

        produto.Skus.Count.ShouldBe(6);
        produto.Skus.Where(s => s.CorId == Azul).Select(s => s.Tamanho).ShouldBe(["P", "M", "G"]);
        produto.Skus.Select(s => s.CodigoDeBarras).Distinct().Count().ShouldBe(6);
        produto.Skus.ShouldAllBe(s => s.Ativo && s.CodigoDeBarras.StartsWith('2'));
        produto.Referencia.ShouldBe("CAM-001");
    }

    [Fact]
    public void Validar_CamposObrigatoriosEPrecos()
    {
        var erros = Produto.Validar(new DadosDoProduto { Referencia = "", Descricao = " ", PrecoVenda = -1, PrecoCusto = -2 });

        erros.ShouldContain(e => e.Contains("referência"));
        erros.ShouldContain(e => e.Contains("descrição"));
        erros.ShouldContain(e => e.Contains("preço"));
        erros.ShouldContain(e => e.Contains("custo"));
    }

    [Fact]
    public void Criar_SemCores_Recusa() =>
        Should.Throw<ArgumentException>(() => Produto.Criar(Dados(), Grade, Tamanhos, [], Gerador(), Agora));

    [Fact]
    public void Markup_PrecoSobreCusto()
    {
        Camiseta().Markup.ShouldBe(2.50m);
        Produto.Criar(Dados() with { PrecoCusto = null }, Grade, Tamanhos, [Azul], Gerador(), Agora).Markup.ShouldBeNull();
    }

    [Fact]
    public void PrecoPorSku_SobrescreveOPrecoDoProduto()
    {
        var produto = Camiseta();
        var gg = produto.Skus.First(s => s.Tamanho == "G");

        produto.AlterarSku(gg.Id, gg.CodigoDeBarras, 69.90m, ativo: true);

        produto.PrecoDe(gg).ShouldBe(69.90m);
        produto.PrecoDe(produto.Skus.First(s => s.Tamanho == "P")).ShouldBe(59.90m);
    }

    [Fact]
    public void AlterarSku_CodigoDoFornecedorEDesativarCombinacao()
    {
        var produto = Camiseta();
        var sku = produto.Skus.First();

        produto.AlterarSku(sku.Id, "7891000315507", null, ativo: false);

        sku.CodigoDeBarras.ShouldBe("7891000315507");
        sku.Ativo.ShouldBeFalse();
        Should.Throw<ArgumentException>(() => produto.AlterarSku(sku.Id, "7891000315508", null, true));
    }

    [Fact]
    public void DefinirCores_AdicionaNovasEDesativaAsRetiradas()
    {
        var produto = Camiseta();
        var vermelho = Guid.NewGuid();

        produto.DefinirCores([Azul, vermelho], Tamanhos, Gerador());

        produto.Skus.Count.ShouldBe(9);
        produto.Skus.Where(s => s.CorId == Preto).ShouldAllBe(s => !s.Ativo);
        produto.Skus.Where(s => s.CorId == vermelho).ShouldAllBe(s => s.Ativo);

        produto.DefinirCores([Azul, Preto, vermelho], Tamanhos, Gerador());
        produto.Skus.Count.ShouldBe(9);
        produto.Skus.Where(s => s.CorId == Preto).ShouldAllBe(s => s.Ativo);
    }

    [Fact]
    public void Atualizar_MudaDadosMasNaoAReferencia()
    {
        var produto = Camiseta();

        produto.Atualizar(Dados() with { Referencia = "OUTRA", Descricao = "Camiseta Gola V", PrecoVenda = 64.90m });

        produto.Referencia.ShouldBe("CAM-001");
        produto.Descricao.ShouldBe("Camiseta Gola V");
        produto.PrecoVenda.ShouldBe(64.90m);
    }
}
