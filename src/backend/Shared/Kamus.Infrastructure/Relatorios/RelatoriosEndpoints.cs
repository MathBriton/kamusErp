using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kamus.Infrastructure.Relatorios;

public static class RelatoriosEndpoints
{
    public sealed record ExportacaoAgendadaResponse(string Mensagem);

    public static IServiceCollection AddKamusRelatorios(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RelatoriosOptions>(configuration.GetSection(RelatoriosOptions.Secao));
        services.AddSingleton<IExportadorDeRelatorio, ExportadorPdf>();
        services.AddSingleton<IExportadorDeRelatorio, ExportadorExcel>();
        services.AddSingleton<IExportadorDeRelatorio, ExportadorCsv>();
        services.AddSingleton<FilaDeExportacao>();
        services.AddSingleton<ArquivosExportados>();
        services.AddHostedService<ExportacaoEmSegundoPlano>();
        return services;
    }

    public static IEndpointRouteBuilder MapKamusRelatorios(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/relatorios").WithTags("Relatórios");

        grupo.MapGet("/{codigo}.{formato}", Exportar)
            .RequireAuthorization()
            .WithSummary("Exporta um relatório (pdf, xlsx ou csv) com os filtros da tela na query string")
            .Produces(StatusCodes.Status200OK)
            .Produces<ExportacaoAgendadaResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Link entregue na notificação "relatório pronto": abre direto no navegador (o id é o segredo, expira).
        grupo.MapGet("/arquivos/{id:guid}", Baixar)
            .AllowAnonymous()
            .WithSummary("Baixa um relatório gerado em segundo plano")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> Exportar(
        string codigo, string formato, HttpContext http,
        IEnumerable<IRelatorio> relatorios, IEnumerable<IExportadorDeRelatorio> exportadores,
        IEmpresaAtiva empresaAtiva, ITenantContext tenant, IUsuarioAtual usuario, IRegistradorDeEventos eventos,
        FilaDeExportacao fila, IOptions<RelatoriosOptions> opcoes, TimeProvider relogio, CancellationToken ct)
    {
        var relatorio = relatorios.FirstOrDefault(r => r.Codigo == codigo);
        var exportador = exportadores.FirstOrDefault(e => string.Equals(e.Formato, formato, StringComparison.OrdinalIgnoreCase));
        if (relatorio is null || exportador is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Relatório não encontrado",
                detail: "Relatório ou formato inexistente. Formatos disponíveis: pdf, xlsx e csv.");
        if (!empresaAtiva.Definida)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empresa não informada", detail: "Selecione uma empresa (header X-Empresa-Id).");
        if (!relatorio.Perfis.Contains(empresaAtiva.Perfil))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: $"Seu perfil não pode exportar \"{relatorio.Titulo}\".");

        var parametros = http.Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var dados = await relatorio.GerarAsync(parametros, ct);
        var agora = relogio.GetUtcNow();
        var cabecalho = new CabecalhoDoRelatorio(empresaAtiva.Nome ?? "", empresaAtiva.Cnpj, usuario.Nome ?? "—", agora);
        var nomeDoArquivo = $"{relatorio.Codigo}-{FormatoBr.ParaBrasilia(agora):yyyyMMdd-HHmm}.{exportador.Formato}";

        await eventos.RegistrarAsync("exportacao",
            $"Exportou \"{relatorio.Titulo}\" em {exportador.Formato.ToUpperInvariant()} ({dados.Linhas.Count} linhas)", ct: ct);

        if (dados.Linhas.Count > opcoes.Value.LimiteSincrono && tenant.TenantId is { } tenantId && usuario.Id is { } usuarioId)
        {
            await fila.EnfileirarAsync(new PedidoDeExportacao(tenantId, usuarioId, empresaAtiva.EmpresaId, dados, cabecalho, exportador.Formato, nomeDoArquivo), ct);
            return Results.Accepted(value: new ExportacaoAgendadaResponse(
                $"Relatório grande ({dados.Linhas.Count} linhas): você será avisado no sininho quando o arquivo estiver pronto."));
        }

        return Results.File(exportador.Exportar(dados, cabecalho), exportador.ContentType, nomeDoArquivo);
    }

    private static IResult Baixar(Guid id, ArquivosExportados arquivos) =>
        arquivos.Obter(id) is { } arquivo
            ? Results.File(arquivo.Conteudo, arquivo.ContentType, arquivo.Nome)
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Arquivo não encontrado",
                detail: "O arquivo não existe ou expirou. Exporte o relatório novamente.");
}
