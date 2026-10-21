namespace Kamus.Api.Infra;

/// <summary>
/// Corpo que não pôde ser lido (JSON malformado, texto vazio num campo de data/número/id) vira 400 com ProblemDetails
/// em português, em vez da página de exceção (Development) ou de um 400 sem corpo (produção).
/// </summary>
internal static class RequisicaoInvalida
{
    public const string Mensagem = "Os dados enviados estão em formato inválido. Confira os campos e tente de novo.";

    public static IApplicationBuilder UseKamusRequisicaoInvalida(this IApplicationBuilder app) =>
        app.Use(async (contexto, proximo) =>
        {
            try
            {
                await proximo(contexto);
            }
            catch (BadHttpRequestException ex) when (!contexto.Response.HasStarted)
            {
                contexto.Response.Clear();
                await Results.Problem(Mensagem, statusCode: ex.StatusCode).ExecuteAsync(contexto);
            }
        });
}
