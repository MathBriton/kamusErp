using System.Net;
using System.Text;
using Kamus.Modules.Notificacoes.Aplicacao;
using Kamus.Modules.Notificacoes.Dominio;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kamus.UnitTests.Notificacoes;

public class DispositivoTests
{
    [Theory]
    [InlineData("ExponentPushToken[abc123]", true)]
    [InlineData("ExpoPushToken[abc123]", true)]
    [InlineData("ExponentPushToken[]", false)]
    [InlineData("token-do-firebase", false)]
    [InlineData(null, false)]
    public void TokenValido_SoTokensDoExpo(string? token, bool valido) => Dispositivo.TokenValido(token).ShouldBe(valido);

    [Fact]
    public void Registrar_PlataformaDesconhecidaOuVazia_FicaDesconhecida()
    {
        Dispositivo.Registrar(Guid.NewGuid(), "ExponentPushToken[x]", " Android ", DateTimeOffset.UtcNow).Plataforma.ShouldBe("android");
        Dispositivo.Registrar(Guid.NewGuid(), "ExponentPushToken[x]", null, DateTimeOffset.UtcNow).Plataforma.ShouldBe("desconhecida");
    }
}

public class EnvioDePushExpoTests
{
    private sealed class Respondedor(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public string? Enviado { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Enviado = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly MensagemDePush Mensagem = new(["ExponentPushToken[a]", "ExponentPushToken[b]"], "Resumo do dia", "Vendas: R$ 100,00", "/");

    [Fact]
    public async Task Enviar_AparelhoNaoRegistrado_DevolveOTokenParaRemover()
    {
        var handler = new Respondedor(HttpStatusCode.OK,
            """{"data":[{"status":"ok","id":"1"},{"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}}]}""");
        var envio = new EnvioDePushExpo(new HttpClient(handler), NullLogger<EnvioDePushExpo>.Instance);

        var invalidos = await envio.EnviarAsync(Mensagem, TestContext.Current.CancellationToken);

        invalidos.ShouldBe(["ExponentPushToken[b]"]);
        var enviado = handler.Enviado.ShouldNotBeNull();
        enviado.ShouldContain("\"to\":\"ExponentPushToken[a]\"");
        enviado.ShouldContain("\"title\":\"Resumo do dia\"");
        enviado.ShouldContain("\"link\":\"/\"");
    }

    [Fact]
    public async Task Enviar_ServicoFora_NaoRemoveNinguem()
    {
        var envio = new EnvioDePushExpo(new HttpClient(new Respondedor(HttpStatusCode.ServiceUnavailable, "{}")), NullLogger<EnvioDePushExpo>.Instance);

        (await envio.EnviarAsync(Mensagem, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }
}
