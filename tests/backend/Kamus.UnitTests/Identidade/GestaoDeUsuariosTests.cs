using Kamus.Modules.Identidade.Dominio;
using Kamus.SharedKernel.Documentos;

namespace Kamus.UnitTests.Identidade;

public class GestaoDeUsuariosTests
{
    private static Usuario NovoUsuario() => Usuario.Criar(Guid.NewGuid(), "Ana", "ana@loja.com");

    [Fact]
    public void SenhaProvisoria_ExigeTrocaNoProximoAcesso()
    {
        var usuario = NovoUsuario();

        usuario.DefinirSenhaProvisoria("hash-provisorio");

        usuario.DeveTrocarSenha.ShouldBeTrue();
        usuario.SenhaHash.ShouldBe("hash-provisorio");
    }

    [Fact]
    public void TrocarSenha_LiberaOAcessoNormal()
    {
        var usuario = NovoUsuario();
        usuario.DefinirSenhaProvisoria("hash-provisorio");

        usuario.TrocarSenha("hash-novo");

        usuario.DeveTrocarSenha.ShouldBeFalse();
        usuario.SenhaHash.ShouldBe("hash-novo");
    }

    [Fact]
    public void DesativarEReativar()
    {
        var usuario = NovoUsuario();

        usuario.Desativar();
        usuario.Ativo.ShouldBeFalse();

        usuario.Reativar();
        usuario.Ativo.ShouldBeTrue();
    }

    [Fact]
    public void Renomear_RemoveEspacosERecusaVazio()
    {
        var usuario = NovoUsuario();

        usuario.Renomear("  Ana Paula ");

        usuario.Nome.ShouldBe("Ana Paula");
        Should.Throw<ArgumentException>(() => usuario.Renomear(" "));
    }

    [Theory]
    [InlineData("curta1", "pelo menos 8")]
    [InlineData("somenteletras", "letras e números")]
    [InlineData("12345678", "letras e números")]
    public void PoliticaDeSenha_RecusaSenhasFracas(string senha, string trecho)
    {
        PoliticaDeSenha.Validar(senha).ShouldNotBeNull().ShouldContain(trecho);
    }

    [Theory]
    [InlineData("Kamus@Dev123")]
    [InlineData("loja2026abc")]
    public void PoliticaDeSenha_AceitaSenhasBoas(string senha)
    {
        PoliticaDeSenha.Validar(senha).ShouldBeNull();
    }

    [Fact]
    public void Empresa_Atualizar_MantemCnpj()
    {
        var empresa = Empresa.Criar("Loja LTDA", "Loja", Cnpj.Parse("11.222.333/0001-81"));

        empresa.Atualizar(" Loja Nova LTDA ", " Loja Nova ");

        empresa.RazaoSocial.ShouldBe("Loja Nova LTDA");
        empresa.NomeFantasia.ShouldBe("Loja Nova");
        empresa.Cnpj.Formatado.ShouldBe("11.222.333/0001-81");
        Should.Throw<ArgumentException>(() => empresa.Atualizar("", "x"));
    }

    [Fact]
    public void AcessoEmpresa_AlterarPerfil()
    {
        var acesso = AcessoEmpresa.Criar(Guid.NewGuid(), Guid.NewGuid(), Perfil.Vendedor);

        acesso.AlterarPerfil(Perfil.Gerente);

        acesso.Perfil.ShouldBe(Perfil.Gerente);
    }
}
