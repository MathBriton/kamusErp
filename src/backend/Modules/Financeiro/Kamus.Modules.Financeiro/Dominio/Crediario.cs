using System.ComponentModel;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Financeiro.Dominio;

/// <summary>Limite de crédito do cliente no crediário (aprovado por Dono/Gerente), válido em todas as empresas do cliente do SaaS.</summary>
[DisplayName("Limite de crediário")]
public sealed class LimiteDeCredito : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ClienteId { get; private set; }
    public string ClienteNome { get; private set; } = "";
    public decimal Limite { get; private set; }

    /// <summary>Bloqueio manual (ex.: cheque devolvido), independente de atraso.</summary>
    public bool Bloqueado { get; private set; }

    public string? Observacao { get; private set; }
    public string? AtualizadoPor { get; private set; }

    private LimiteDeCredito() { }

    public static LimiteDeCredito Definir(Guid clienteId, string clienteNome, decimal limite, bool bloqueado, string? observacao, string? usuario)
    {
        var l = new LimiteDeCredito { Id = Guid.NewGuid(), ClienteId = clienteId };
        l.Atualizar(clienteNome, limite, bloqueado, observacao, usuario);
        return l;
    }

    public void Atualizar(string clienteNome, decimal limite, bool bloqueado, string? observacao, string? usuario)
    {
        if (limite < 0) throw new ArgumentException("O limite não pode ser negativo.", nameof(limite));
        (ClienteNome, Limite, Bloqueado, AtualizadoPor) = (clienteNome.Trim()[..Math.Min(clienteNome.Trim().Length, 150)], decimal.Round(limite, 2), bloqueado, usuario);
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()[..Math.Min(observacao.Trim().Length, 300)];
    }
}

/// <summary>Regras do crediário do cliente do SaaS: multa e juros por atraso e dias de atraso que bloqueiam novas compras.</summary>
[DisplayName("Configuração do crediário")]
public sealed class ConfiguracaoDoCrediario : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Multa por atraso (% do saldo, uma vez). O CDC limita a 2%.</summary>
    public decimal MultaPercentual { get; private set; } = 2m;

    /// <summary>Juros de mora ao mês, pro rata dia.</summary>
    public decimal JurosMensalPercentual { get; private set; } = 1m;

    /// <summary>Atraso (em dias) a partir do qual novas compras no crediário são bloqueadas.</summary>
    public int DiasParaBloquear { get; private set; } = 15;

    private ConfiguracaoDoCrediario() { }

    public static ConfiguracaoDoCrediario Padrao() => new() { Id = Guid.NewGuid() };

    public void Definir(decimal multaPercentual, decimal jurosMensalPercentual, int diasParaBloquear)
    {
        if (multaPercentual is < 0 or > 2) throw new ArgumentException("A multa por atraso vai de 0% a 2% (limite do CDC).", nameof(multaPercentual));
        if (jurosMensalPercentual is < 0 or > 10) throw new ArgumentException("Os juros vão de 0% a 10% ao mês.", nameof(jurosMensalPercentual));
        if (diasParaBloquear is < 1 or > 365) throw new ArgumentException("Dias para bloquear: de 1 a 365.", nameof(diasParaBloquear));
        (MultaPercentual, JurosMensalPercentual, DiasParaBloquear) = (multaPercentual, jurosMensalPercentual, diasParaBloquear);
    }
}

/// <summary>Multa e juros sugeridos para receber uma parcela atrasada (o caixa pode ajustar na baixa).</summary>
public sealed record Encargos(int DiasDeAtraso, decimal Multa, decimal Juros)
{
    public static Encargos Calcular(decimal saldo, DateOnly vencimento, DateOnly data, ConfiguracaoDoCrediario config)
    {
        var dias = Math.Max(0, data.DayNumber - vencimento.DayNumber);
        if (dias == 0 || saldo <= 0) return new Encargos(dias, 0m, 0m);
        return new Encargos(dias, Arredondar(saldo * config.MultaPercentual / 100), Arredondar(saldo * config.JurosMensalPercentual / 100 / 30 * dias));
    }

    private static decimal Arredondar(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

public enum FaixaDeAtraso
{
    AVencer,
    Ate30,
    Ate60,
    Ate90,
    Acima90,
}

/// <summary>Análise de crédito do cliente e faixas de atraso (aging).</summary>
public static class Credito
{
    public static SituacaoDoCredito Avaliar(LimiteDeCredito? limite, decimal emAberto, int diasDeAtraso, ConfiguracaoDoCrediario config)
    {
        var valor = limite?.Limite ?? 0m;
        var disponivel = Math.Max(0, valor - emAberto);
        var motivo = limite is null ? "Cliente sem limite de crediário aprovado."
            : limite.Bloqueado ? $"Crediário bloqueado{(limite.Observacao is null ? "." : $": {limite.Observacao}")}"
            : diasDeAtraso > config.DiasParaBloquear ? $"Parcela atrasada há {diasDeAtraso} dias (bloqueia com {config.DiasParaBloquear})."
            : null;
        return new SituacaoDoCredito(valor, emAberto, disponivel, diasDeAtraso, motivo is not null, motivo);
    }

    public static FaixaDeAtraso Faixa(int diasDeAtraso) => diasDeAtraso switch
    {
        <= 0 => FaixaDeAtraso.AVencer,
        <= 30 => FaixaDeAtraso.Ate30,
        <= 60 => FaixaDeAtraso.Ate60,
        <= 90 => FaixaDeAtraso.Ate90,
        _ => FaixaDeAtraso.Acima90,
    };
}
