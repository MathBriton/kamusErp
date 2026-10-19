using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Kamus.Modules.Notificacoes.Aplicacao;

/// <param name="Link">Rota do app/web aberta ao tocar (vai em <c>data.link</c>).</param>
public sealed record MensagemDePush(IReadOnlyList<string> Tokens, string Titulo, string Corpo, string? Link);

/// <summary>Envia push para os aparelhos. Devolve os tokens que não existem mais (app desinstalado), para tirar da lista.</summary>
public interface IEnvioDePush
{
    Task<IReadOnlyList<string>> EnviarAsync(MensagemDePush mensagem, CancellationToken ct);
}

/// <summary>Com <c>Push:Habilitado=false</c> (testes, ambientes sem internet): não envia nada.</summary>
internal sealed class EnvioDePushDesligado : IEnvioDePush
{
    public Task<IReadOnlyList<string>> EnviarAsync(MensagemDePush mensagem, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
}

/// <summary>Expo Push API (https://exp.host/--/api/v2/push/send): um envio por aparelho na mesma requisição.</summary>
internal sealed class EnvioDePushExpo(HttpClient http, ILogger<EnvioDePushExpo> log) : IEnvioDePush
{
    public static readonly Uri Endereco = new("https://exp.host/--/api/v2/push/send");
    private const int TamanhoMaximoDoCorpo = 400;

    private sealed record Retorno(List<Ticket>? Data);
    private sealed record Ticket(string? Status, string? Message, JsonElement? Details);

    public async Task<IReadOnlyList<string>> EnviarAsync(MensagemDePush m, CancellationToken ct)
    {
        var corpo = m.Corpo.Length <= TamanhoMaximoDoCorpo ? m.Corpo : string.Concat(m.Corpo.AsSpan(0, TamanhoMaximoDoCorpo - 1), "…");
        var envios = m.Tokens.Select(t => new { to = t, title = m.Titulo, body = corpo, sound = "default", data = new { link = m.Link } }).ToList();
        using var resposta = await http.PostAsJsonAsync(Endereco, envios, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            log.FalhaNoPush((int)resposta.StatusCode);
            return [];
        }
        var tickets = (await resposta.Content.ReadFromJsonAsync<Retorno>(ct))?.Data ?? [];
        return tickets.Select((t, i) => (t, i))
            .Where(x => x.i < m.Tokens.Count && x.t.Status == "error"
                && x.t.Details is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("error", out var e) && e.GetString() == "DeviceNotRegistered")
            .Select(x => m.Tokens[x.i]).ToList();
    }
}
