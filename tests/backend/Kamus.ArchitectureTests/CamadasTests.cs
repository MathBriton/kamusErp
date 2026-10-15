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
                typeof(Kamus.Modules.Notificacoes.ModuloNotificacoes).Assembly,
                typeof(Kamus.Modules.Licenciamento.ModuloLicenciamento).Assembly,
                typeof(Kamus.Modules.Cadastros.ModuloCadastros).Assembly,
                typeof(Kamus.Modules.Estoque.ModuloEstoque).Assembly,
                typeof(Kamus.Modules.Financeiro.ModuloFinanceiro).Assembly])
            .ShouldNot().HaveDependencyOn("Kamus.Api")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Modulos_NaoDependemUnsDosOutros()
    {
        // Comunicação entre módulos só pelo SharedKernel (ex.: INotificador, IRegistradorDeEventos).
        string[] modulos = ["Kamus.Modules.Identidade", "Kamus.Modules.Notificacoes", "Kamus.Modules.Licenciamento", "Kamus.Modules.Cadastros", "Kamus.Modules.Estoque", "Kamus.Modules.Financeiro"];
        System.Reflection.Assembly[] assemblies = [
            typeof(Kamus.Modules.Identidade.ModuloIdentidade).Assembly,
            typeof(Kamus.Modules.Notificacoes.ModuloNotificacoes).Assembly,
            typeof(Kamus.Modules.Licenciamento.ModuloLicenciamento).Assembly,
            typeof(Kamus.Modules.Cadastros.ModuloCadastros).Assembly,
            typeof(Kamus.Modules.Estoque.ModuloEstoque).Assembly,
            typeof(Kamus.Modules.Financeiro.ModuloFinanceiro).Assembly];

        foreach (var assembly in assemblies)
        {
            var proprio = assembly.GetName().Name!;
            var resultado = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny([.. modulos.Where(m => m != proprio)])
                .GetResult();

            resultado.IsSuccessful.ShouldBeTrue($"{proprio}: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
        }
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
