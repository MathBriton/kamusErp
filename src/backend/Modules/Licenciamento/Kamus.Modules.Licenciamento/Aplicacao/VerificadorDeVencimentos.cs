using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>
/// Avisa os donos de cada cliente: licença vencendo (7, 3, 1 e 0 dias antes), mensalidade vencida (carência) e
/// suspensão. Cada aviso sai uma única vez (fica marcado na licença), então pode rodar quantas vezes quiser.
/// O estado em si não depende desta rotina: é calculado pelas datas.
/// </summary>
public sealed class VerificadorDeVencimentos(LicenciamentoDbContext db, AvisoAosDonos avisos, ILogger<VerificadorDeVencimentos> log)
{
    private static readonly int[] DiasDeAviso = [7, 3, 1, 0];

    public async Task ExecutarAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var licencas = await db.Licencas.Where(l => l.CanceladaEm == null).ToListAsync(ct);
        foreach (var licenca in licencas)
        {
            if (AvisoPara(licenca, hoje) is not { } aviso || !licenca.MarcarAviso(aviso.Chave)) continue;

            try
            {
                await avisos.EnviarAsync(licenca.TenantId, aviso.Tipo, aviso.Titulo, aviso.Mensagem, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.FalhaAoAvisar(ex, licenca.TenantId);
                db.Entry(licenca).State = EntityState.Unchanged;
            }
        }
    }

    private sealed record Aviso(string Chave, string Tipo, string Titulo, string Mensagem);

    private static Aviso? AvisoPara(Licenca l, DateOnly hoje)
    {
        var estado = l.EstadoEm(hoje);
        var vencimento = SituacaoDaLicenca.Data(l.Vencimento);
        switch (estado)
        {
            case EstadoLicenca.Trial or EstadoLicenca.Ativa:
                var dias = l.Vencimento!.Value.DayNumber - hoje.DayNumber;
                if (!DiasDeAviso.Contains(dias)) return null;
                var quando = dias switch { 0 => "vence hoje", 1 => "vence amanhã", _ => $"vence em {dias} dias" };
                var oque = estado == EstadoLicenca.Trial ? "Seu período de avaliação" : "Sua licença";
                return new Aviso($"vencendo:{l.Vencimento:yyyy-MM-dd}:{dias}", "licenca_vencendo", $"{oque} {quando}",
                    $"Vencimento em {vencimento}. Depois disso há {l.DiasDeCarencia} dias de carência antes da suspensão.");
            case EstadoLicenca.Carencia:
                return new Aviso($"carencia:{l.Vencimento:yyyy-MM-dd}", "licenca_em_carencia", "Mensalidade vencida",
                    $"A mensalidade venceu em {vencimento}. O uso continua normal até {SituacaoDaLicenca.Data(l.FimDaCarencia)}; depois, o acesso é suspenso.");
            case EstadoLicenca.Suspensa:
                return new Aviso($"suspensa:{l.Vencimento:yyyy-MM-dd}", "licenca_suspensa", "Licença suspensa",
                    "O acesso foi suspenso por falta de pagamento. Seus dados estão preservados; fale com o suporte para regularizar.");
            default:
                return null;
        }
    }
}
