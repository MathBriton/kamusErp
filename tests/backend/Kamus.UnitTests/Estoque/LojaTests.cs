using Kamus.Modules.Estoque.Dominio;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

public class LojaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_NomeAparadoEAtiva()
    {
        var empresa = Guid.NewGuid();

        var loja = Loja.Criar(empresa, "  Loja Centro ", Agora);

        loja.ShouldSatisfyAllConditions(
            () => loja.Nome.ShouldBe("Loja Centro"),
            () => loja.EmpresaId.ShouldBe(empresa),
            () => loja.Ativa.ShouldBeTrue(),
            () => loja.CriadaEm.ShouldBe(Agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Erro(string nome) => Should.Throw<ArgumentException>(() => Loja.Criar(Guid.NewGuid(), nome, Agora));

    [Fact]
    public void Criar_NomeLongo_Erro() => Should.Throw<ArgumentException>(() => Loja.Criar(Guid.NewGuid(), new string('x', 61), Agora));

    [Fact]
    public void DesativarEReativar()
    {
        var loja = Loja.Criar(Guid.NewGuid(), "Loja", Agora);

        loja.Desativar();
        loja.Ativa.ShouldBeFalse();
        loja.Reativar();
        loja.Ativa.ShouldBeTrue();
    }
}
