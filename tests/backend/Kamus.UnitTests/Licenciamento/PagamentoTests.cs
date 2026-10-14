using Kamus.Modules.Licenciamento.Dominio;

namespace Kamus.UnitTests.Licenciamento;

public class PagamentoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateOnly Hoje = new(2026, 10, 14);
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static Licenca PagaAte(DateOnly ate)
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje.AddDays(-90), Agora);
        licenca.RegistrarPagamento(ate);
        return licenca;
    }

    [Fact]
    public void Pagar_EmDia_EstendeAPartirDoVencimento()
    {
        var licenca = PagaAte(Hoje.AddDays(3));

        var pagamento = licenca.Pagar(199.90m, meses: 1, Hoje, "Pix", null, "admin", Agora);

        licenca.PagoAte.ShouldBe(Hoje.AddDays(3).AddMonths(1));
        pagamento.EstendeuAte.ShouldBe(licenca.PagoAte!.Value);
        pagamento.TenantId.ShouldBe(Tenant);
        pagamento.Valor.ShouldBe(199.90m);
    }

    [Fact]
    public void Pagar_NaCarencia_ContinuaDoVencimento()
    {
        var licenca = PagaAte(Hoje.AddDays(-2));

        licenca.Pagar(199.90m, 1, Hoje, null, null, "admin", Agora);

        licenca.PagoAte.ShouldBe(Hoje.AddDays(-2).AddMonths(1));
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Ativa);
    }

    [Fact]
    public void Pagar_Suspensa_RecomecaDeHojeELiberaNaHora()
    {
        var licenca = PagaAte(Hoje.AddDays(-40));

        licenca.Pagar(199.90m, 1, Hoje, null, null, "admin", Agora);

        licenca.PagoAte.ShouldBe(Hoje.AddMonths(1));
        licenca.EstadoEm(Hoje).ShouldBe(EstadoLicenca.Ativa);
    }

    [Fact]
    public void Pagar_NoTrial_ComecaDepoisDoTrial()
    {
        var licenca = Licenca.CriarTrial(Tenant, Hoje, Agora);

        licenca.Pagar(199.90m, 1, Hoje, null, null, "admin", Agora);

        licenca.PagoAte.ShouldBe(Hoje.AddDays(14).AddMonths(1));
    }

    [Fact]
    public void Pagar_VariosMeses()
    {
        var licenca = PagaAte(Hoje);

        licenca.Pagar(599.70m, 3, Hoje, null, null, "admin", Agora);

        licenca.PagoAte.ShouldBe(Hoje.AddMonths(3));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(199.9, 0)]
    [InlineData(199.9, 13)]
    public void Pagar_ValoresInvalidos_Recusa(double valor, int meses)
    {
        var licenca = PagaAte(Hoje);

        Should.Throw<ArgumentOutOfRangeException>(() => licenca.Pagar((decimal)valor, meses, Hoje, null, null, "admin", Agora));
    }

    [Fact]
    public void Pagar_Cancelada_Recusa()
    {
        var licenca = PagaAte(Hoje);
        licenca.Cancelar(Agora);

        Should.Throw<InvalidOperationException>(() => licenca.Pagar(199.90m, 1, Hoje, null, null, "admin", Agora));
    }
}
