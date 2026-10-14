using Kamus.Modules.Identidade.Dominio;

namespace Kamus.UnitTests.Identidade;

public class UsuarioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static Usuario NovoUsuario() => Usuario.Criar(Guid.NewGuid(), "Admin Demo", "admin@kamus.local");

    [Fact]
    public void Criar_NormalizaEmailEmMinusculasSemEspacos()
    {
        var usuario = Usuario.Criar(Guid.NewGuid(), " Ana ", "  Ana.Silva@Loja.COM ");

        usuario.Email.ShouldBe("ana.silva@loja.com");
        usuario.Nome.ShouldBe("Ana");
        usuario.Ativo.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("@semusuario.com")]
    public void Criar_EmailInvalido_LancaExcecao(string email)
    {
        Should.Throw<ArgumentException>(() => Usuario.Criar(Guid.NewGuid(), "Ana", email));
    }

    [Fact]
    public void RegistrarFalha_QuatroVezes_AindaNaoBloqueia()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < 4; i++) usuario.RegistrarFalhaDeLogin(Agora);

        usuario.EstaBloqueado(Agora).ShouldBeFalse();
    }

    [Fact]
    public void RegistrarFalha_NaQuintaVez_BloqueiaPorQuinzeMinutos()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < Usuario.MaximoDeFalhas; i++) usuario.RegistrarFalhaDeLogin(Agora);

        usuario.EstaBloqueado(Agora).ShouldBeTrue();
        usuario.BloqueadoAte.ShouldBe(Agora + Usuario.TempoDeBloqueio);
        usuario.EstaBloqueado(Agora.AddMinutes(14)).ShouldBeTrue();
        usuario.EstaBloqueado(Agora.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void RegistrarSucesso_ZeraAsFalhas()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < 4; i++) usuario.RegistrarFalhaDeLogin(Agora);

        usuario.RegistrarLoginComSucesso(Agora);
        usuario.RegistrarFalhaDeLogin(Agora);

        usuario.EstaBloqueado(Agora).ShouldBeFalse();
        usuario.UltimoLoginEm.ShouldBe(Agora);
    }

    [Fact]
    public void AposBloqueioExpirar_ContagemRecomeca()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < Usuario.MaximoDeFalhas; i++) usuario.RegistrarFalhaDeLogin(Agora);

        var depois = Agora.AddMinutes(20);
        usuario.RegistrarFalhaDeLogin(depois);

        usuario.EstaBloqueado(depois).ShouldBeFalse();
    }
}
