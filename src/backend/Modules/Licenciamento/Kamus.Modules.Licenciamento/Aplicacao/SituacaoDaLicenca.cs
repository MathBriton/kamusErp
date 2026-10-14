using System.Globalization;
using Kamus.Modules.Licenciamento.Dominio;

namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>Situação da licença como o cliente vê (banner, tela de bloqueio, Configurações → Licença).</summary>
public sealed record SituacaoDaLicenca(
    string Plano, string Estado, DateOnly? Vencimento, DateOnly? FimDaCarencia, int? DiasParaVencer,
    decimal ValorMensal, string Mensagem, int? LimiteDeCnpjs, int? LimiteDeLojas, int? LimiteDeUsuarios)
{
    public const string CodigoSuspensa = "LICENCA_SUSPENSA";
    public const string CodigoCancelada = "LICENCA_CANCELADA";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static SituacaoDaLicenca De(Licenca l, DateOnly hoje)
    {
        var estado = l.EstadoEm(hoje);
        int? dias = l.Vencimento is { } v ? v.DayNumber - hoje.DayNumber : null;
        return new SituacaoDaLicenca(l.Plano, estado.ToString(), l.Vencimento, l.FimDaCarencia, dias, l.ValorMensal,
            MensagemPara(l, estado), l.LimiteDeCnpjs, l.LimiteDeLojas, l.LimiteDeUsuarios);
    }

    internal static string Data(DateOnly? data) => data?.ToString("dd/MM/yyyy", PtBr) ?? "—";

    private static string MensagemPara(Licenca l, EstadoLicenca estado) => estado switch
    {
        EstadoLicenca.Trial => $"Período de avaliação até {Data(l.Vencimento)}.",
        EstadoLicenca.Ativa => $"Licença em dia. Próximo vencimento em {Data(l.Vencimento)}.",
        EstadoLicenca.Carencia => $"A mensalidade venceu em {Data(l.Vencimento)}. Você está no período de carência até {Data(l.FimDaCarencia)}: " +
                                  "o uso continua normal, mas regularize para evitar a suspensão.",
        EstadoLicenca.Cancelada => "Esta licença foi cancelada. Seus dados estão preservados e podem ser exportados. Fale com o suporte para reativar.",
        _ => $"Licença suspensa por falta de pagamento (carência terminou em {Data(l.FimDaCarencia)}). " +
             "Seus dados estão preservados e podem ser exportados. Fale com o suporte para regularizar — a liberação é imediata.",
    };
}
