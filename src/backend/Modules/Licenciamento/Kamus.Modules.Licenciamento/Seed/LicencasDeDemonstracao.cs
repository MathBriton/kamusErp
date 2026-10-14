using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Licenciamento.Seed;

/// <summary>
/// Só em Development: todo cliente sem licença ganha uma, já paga (trial encerrado e uma mensalidade registrada),
/// vencendo daqui a ~3 semanas. Assim o ambiente de demonstração abre com a licença Ativa.
/// </summary>
internal static class LicencasDeDemonstracao
{
    public const decimal ValorMensal = 199.90m;

    public static async Task CriarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var escopo = services.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<LicenciamentoDbContext>();
        var relogio = sp.GetRequiredService<TimeProvider>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kamus.Licenciamento");
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var agora = relogio.GetUtcNow();

        var comLicenca = await db.Licencas.Select(l => l.TenantId).ToListAsync(ct);
        foreach (var cliente in await sp.GetRequiredService<IDiretorioDeClientes>().ListarAsync(ct))
        {
            if (comLicenca.Contains(cliente.TenantId)) continue;

            var licenca = Licenca.CriarTrial(cliente.TenantId, hoje.AddDays(-20), agora, ValorMensal);
            db.Licencas.Add(licenca);
            db.Pagamentos.Add(licenca.Pagar(ValorMensal, 1, hoje.AddDays(-7), "Pix", "Mensalidade de demonstração", "seed", agora));
            log.LicencaDeDemonstracao(cliente.TenantId);
        }
        await db.SaveChangesAsync(ct);
    }
}
