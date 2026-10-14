using Kamus.Modules.Cadastros.Dominio;

namespace Kamus.UnitTests.Cadastros;

public class VendedorTests
{
    [Fact]
    public void Criar_ComComissao()
    {
        var vendedor = Vendedor.Criar(" Ana ", 3.5m, usuarioId: null, telefone: "11 98765-4321");

        vendedor.Nome.ShouldBe("Ana");
        vendedor.ComissaoPercentual.ShouldBe(3.5m);
        vendedor.Telefone.ShouldBe("11987654321");
        vendedor.Ativo.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Comissao_ForaDe0a100_Recusa(double comissao) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Vendedor.Criar("Ana", (decimal)comissao, null, null));

    [Fact]
    public void AtualizarInativarReativar()
    {
        var vendedor = Vendedor.Criar("Ana", 3m, null, null);
        var usuario = Guid.NewGuid();

        vendedor.Atualizar("Ana Paula", 4m, usuario, null);
        vendedor.Inativar();

        vendedor.Nome.ShouldBe("Ana Paula");
        vendedor.UsuarioId.ShouldBe(usuario);
        vendedor.Ativo.ShouldBeFalse();
        vendedor.Reativar();
        vendedor.Ativo.ShouldBeTrue();
    }
}
