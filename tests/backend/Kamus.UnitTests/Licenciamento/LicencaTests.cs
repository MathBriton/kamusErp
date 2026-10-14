using Kamus.Modules.Licenciamento.Dominio;

namespace Kamus.UnitTests.Licenciamento;

public class LicencaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateOnly Hoje = new(2026, 10, 14);
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static Licenca Paga(DateOnly ate)
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje.AddDays(-60), Agora);
        licenca.RegistrarPagamento(ate);
        return licenca;
    }

    [Fact]
    public void ClienteNovo_ComecaEmTrialDe14Dias()
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje, Agora);

        licenca.TrialAte.ShouldBe(Hoje.AddDays(14));
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Trial);
        licenca.EstadoEm(Hoje.AddDays(14)).ShouldBe(EstadoLicenca.Trial);
        licenca.Plano.ShouldBe("Piloto");
        licenca.DiasDeCarencia.ShouldBe(5);
    }

    [Theory]
    [InlineData(0, EstadoLicenca.Ativa)]       // vence hoje: ainda ativa
    [InlineData(1, EstadoLicenca.Carencia)]    // venceu ontem
    [InlineData(5, EstadoLicenca.Carencia)]    // último dia de carência
    [InlineData(6, EstadoLicenca.Suspensa)]
    [InlineData(40, EstadoLicenca.Suspensa)]
    public void Estado_DependeDosDiasDesdeOVencimento(int diasDepoisDoVencimento, EstadoLicenca esperado)
    {
        var licenca = Paga(Hoje);

        licenca.EstadoEm(Hoje.AddDays(diasDepoisDoVencimento)).ShouldBe(esperado);
    }

    [Fact]
    public void TrialVencido_EntraEmCarenciaEDepoisSuspende()
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje, Agora);

        licenca.EstadoEm(Hoje.AddDays(15)).ShouldBe(EstadoLicenca.Carencia);
        licenca.EstadoEm(Hoje.AddDays(20)).ShouldBe(EstadoLicenca.Suspensa);
    }

    [Fact]
    public void Pagamento_LiberaNaHoraUmaLicencaSuspensa()
    {
        var licenca = Paga(Hoje.AddDays(-30));
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Suspensa);

        licenca.RegistrarPagamento(Hoje.AddMonths(1));

        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Ativa);
        licenca.Vencimento.ShouldBe(Hoje.AddMonths(1));
    }

    [Fact]
    public void Cancelada_ValeMaisQueQualquerData_EReativarVoltaAoEstadoDasDatas()
    {
        var licenca = Paga(Hoje.AddMonths(1));

        licenca.Cancelar(Agora);
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Cancelada);

        licenca.Reativar();
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Ativa);
    }

    [Fact]
    public void FimDaCarencia_EhOVencimentoMaisOsDiasDeCarencia()
    {
        Paga(Hoje).FimDaCarencia.ShouldBe(Hoje.AddDays(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LimitesDoPlano_NaoAceitamZeroOuNegativo(int limite)
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje, Agora);

        Should.Throw<ArgumentOutOfRangeException>(() => licenca.DefinirLimites(limite, null, null));
    }

    [Fact]
    public void LimitesDoPlano_NuloSignificaSemLimite()
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje, Agora);

        licenca.DefinirLimites(2, null, 5);

        (licenca.LimiteDeCnpjs, licenca.LimiteDeLojas, licenca.LimiteDeUsuarios).ShouldBe((2, (int?)null, 5));
    }
}
