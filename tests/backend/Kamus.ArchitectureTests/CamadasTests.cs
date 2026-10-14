using NetArchTest.Rules;

namespace Kamus.ArchitectureTests;

public class CamadasTests
{
    private static readonly System.Reflection.Assembly SharedKernel = typeof(Kamus.SharedKernel.MultiTenancy.ITenantOwned).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(Kamus.Infrastructure.Persistence.KamusDbContext).Assembly;

    [Fact]
    public void SharedKernel_NaoDependeDeInfraestruturaNemDeFrameworks()
    {
        var resultado = Types.InAssembly(SharedKernel)
            .ShouldNot().HaveDependencyOnAny("Kamus.Infrastructure", "Kamus.Api", "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Modulos_NaoDependemDaApi()
    {
        var resultado = Types.InAssemblies([
                typeof(Kamus.Modules.Identidade.ModuloIdentidade).Assembly,
                typeof(Kamus.Modules.Notificacoes.ModuloNotificacoes).Assembly])
            .ShouldNot().HaveDependencyOn("Kamus.Api")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Modulos_NaoDependemUnsDosOutros()
    {
        // Comunicação entre módulos só pelo SharedKernel (ex.: INotificador, IRegistradorDeEventos).
        var resultado = Types.InAssembly(typeof(Kamus.Modules.Identidade.ModuloIdentidade).Assembly)
            .ShouldNot().HaveDependencyOn("Kamus.Modules.Notificacoes")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_NaoDependeDaApi()
    {
        var resultado = Types.InAssembly(Infrastructure)
            .ShouldNot().HaveDependencyOn("Kamus.Api")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
