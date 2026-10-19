using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Indicadores.Dominio;

/// <summary>
/// Como cada usuário recebe o resumo diário no sininho: horário (Brasília), dias da semana e blocos de conteúdo.
/// Quem nunca configurou usa o padrão (<see cref="Padrao"/>): ligado para o Dono, desligado para os demais.
/// </summary>
[DisplayName("Preferências do resumo diário")]
public sealed class PreferenciasDoResumo : ITenantOwned
{
    public const int HoraPadrao = 20;
    public const int TodosOsDias = 0b111_1111;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public bool Ativo { get; private set; }
    public int Hora { get; private set; }

    /// <summary>Dias da semana em bits: bit 0 = domingo … bit 6 = sábado.</summary>
    public int Dias { get; private set; }

    public bool Vendas { get; private set; }
    public bool Financeiro { get; private set; }
    public bool Estoque { get; private set; }
    public bool Condicionais { get; private set; }
    public bool Crediario { get; private set; }
    public bool EnviarSemMovimento { get; private set; }

    [NaoAuditar]
    public DateOnly? UltimoEnvio { get; private set; }

    private PreferenciasDoResumo() { }

    public static PreferenciasDoResumo Padrao(Guid usuarioId, bool dono) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        Ativo = dono,
        Hora = HoraPadrao,
        Dias = TodosOsDias,
        Vendas = true,
        Financeiro = true,
        Estoque = true,
        Condicionais = true,
        Crediario = true,
    };

    public static int DiasEmBits(IEnumerable<int> dias) => dias.Distinct().Aggregate(0, (bits, d) => bits | (1 << d));

    public IReadOnlyList<int> DiasDaSemana() => Enumerable.Range(0, 7).Where(d => (Dias & (1 << d)) != 0).ToList();

    /// <exception cref="ArgumentException">Hora fora de 0–23, dia fora de 0–6 ou nenhum dia escolhido.</exception>
    public void Alterar(bool ativo, int hora, IReadOnlyCollection<int> dias, bool vendas, bool financeiro, bool estoque, bool condicionais, bool crediario,
        bool enviarSemMovimento)
    {
        if (hora is < 0 or > 23) throw new ArgumentException("A hora vai de 0 a 23.", nameof(hora));
        if (dias.Any(d => d is < 0 or > 6)) throw new ArgumentException("Os dias vão de 0 (domingo) a 6 (sábado).", nameof(dias));
        if (dias.Count == 0) throw new ArgumentException("Escolha ao menos um dia da semana.", nameof(dias));
        (Ativo, Hora, Dias, Vendas, Financeiro, Estoque, Condicionais, Crediario, EnviarSemMovimento) =
            (ativo, hora, DiasEmBits(dias), vendas, financeiro, estoque, condicionais, crediario, enviarSemMovimento);
    }

    /// <summary>Hora de enviar: ligado, dia escolhido, já passou do horário e ainda não enviou hoje.</summary>
    public bool DeveEnviar(DateTime agoraEmBrasilia) =>
        Ativo && (Dias & (1 << (int)agoraEmBrasilia.DayOfWeek)) != 0 && agoraEmBrasilia.Hour >= Hora
        && UltimoEnvio != DateOnly.FromDateTime(agoraEmBrasilia);

    public void MarcarEnvio(DateOnly dia) => UltimoEnvio = dia;
}
