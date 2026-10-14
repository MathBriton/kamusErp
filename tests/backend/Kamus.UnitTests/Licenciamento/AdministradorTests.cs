using Kamus.Modules.Licenciamento.Dominio;

namespace Kamus.UnitTests.Licenciamento;

public class AdministradorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_NormalizaEmail()
    {
        var admin = Administrador.Criar("Super Admin", "  SuperAdmin@Kamus.Local ", Agora);

        admin.Email.ShouldBe("superadmin@kamus.local");
        admin.Ativo.ShouldBeTrue();
    }

    [Fact]
    public void Criar_EmailInvalido_Recusa() =>
        Should.Throw<ArgumentException>(() => Administrador.Criar("Super Admin", "sem-arroba", Agora));

    [Fact]
    public void CincoFalhas_Bloqueia15Minutos()
    {
        var admin = Administrador.Criar("Super Admin", "sa@kamus.local", Agora);

        for (var i = 0; i < 5; i++) admin.RegistrarFalhaDeLogin(Agora);

        admin.EstaBloqueado(Agora.AddMinutes(14)).ShouldBeTrue();
        admin.EstaBloqueado(Agora.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void LoginComSucesso_ZeraFalhas()
    {
        var admin = Administrador.Criar("Super Admin", "sa@kamus.local", Agora);
        for (var i = 0; i < 4; i++) admin.RegistrarFalhaDeLogin(Agora);

        admin.RegistrarLoginComSucesso(Agora);
        admin.RegistrarFalhaDeLogin(Agora);

        admin.EstaBloqueado(Agora).ShouldBeFalse();
        admin.UltimoLoginEm.ShouldBe(Agora);
    }
}
