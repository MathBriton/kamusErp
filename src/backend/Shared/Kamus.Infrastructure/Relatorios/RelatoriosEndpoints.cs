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

    public sealed record ParametroDto(string Nome, string Rotulo, string Tipo, string? Padrao, IReadOnlyList<OpcaoDoParametro>? Opcoes);
    public sealed record ItemDoCatalogo(string Codigo, string Titulo, string Area, string? Descricao, IReadOnlyList<ParametroDto> Parametros);
    public sealed record ColunaDto(string Titulo, string Tipo, bool Totalizar);

    /// <param name="TotalDeLinhas">Linhas do relatório; a tela recebe no máximo <see cref="LimiteDaTela"/> (a exportação traz todas).</param>
    public sealed record DadosNaTela(string Titulo, IReadOnlyList<ColunaDto> Colunas, IReadOnlyList<IReadOnlyList<object?>> Linhas, IReadOnlyList<FiltroAplicado> Filtros,
        int TotalDeLinhas);

    public const int LimiteDaTela = 500;

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

        grupo.MapGet("/", Catalogo)
            .RequireAuthorization()
            .WithSummary("Central de relatórios: os que o perfil na empresa ativa pode gerar, com área, descrição e filtros")
            .Produces<IReadOnlyList<ItemDoCatalogo>>();

        grupo.MapGet("/{codigo}/dados", Visualizar)
            .RequireAuthorization()
            .WithSummary("Os dados do relatório para mostrar na tela (até 500 linhas), com os filtros na query string")
            .Produces<DadosNaTela>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static IResult? Barrar(IRelatorio? relatorio, IEmpresaAtiva empresaAtiva)
    {
        if (relatorio is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Relatório não encontrado", detail: "Relatório inexistente.");
        if (!empresaAtiva.Definida)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empresa não informada", detail: "Selecione uma empresa (header X-Empresa-Id).");
        if (!relatorio.Perfis.Contains(empresaAtiva.Perfil))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: $"Seu perfil não pode gerar \"{relatorio.Titulo}\".");
        return null;
    }

    private static Dictionary<string, string?> Parametros(HttpContext http) =>
        http.Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    private static IResult Catalogo(IEnumerable<IRelatorio> relatorios, IEmpresaAtiva empresaAtiva)
    {
        if (!empresaAtiva.Definida)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empresa não informada", detail: "Selecione uma empresa (header X-Empresa-Id).");
        return Results.Ok(relatorios.Where(r => r.NaCentral && r.Perfis.Contains(empresaAtiva.Perfil))
            .OrderBy(r => r.Area, StringComparer.CurrentCulture).ThenBy(r => r.Titulo, StringComparer.CurrentCulture)
            .Select(r => new ItemDoCatalogo(r.Codigo, r.Titulo, r.Area, r.Descricao,
                r.Parametros.Select(p => new ParametroDto(p.Nome, p.Rotulo, p.Tipo.ToString(), p.Padrao, p.Opcoes)).ToList()))
            .ToList());
    }

    private static async Task<IResult> Visualizar(string codigo, HttpContext http, IEnumerable<IRelatorio> relatorios, IEmpresaAtiva empresaAtiva, CancellationToken ct)
    {
        var relatorio = relatorios.FirstOrDefault(r => r.Codigo == codigo);
        if (Barrar(relatorio, empresaAtiva) is { } barrado) return barrado;
        var dados = await relatorio!.GerarAsync(Parametros(http), ct);
        return Results.Ok(new DadosNaTela(dados.Titulo, dados.Colunas.Select(c => new ColunaDto(c.Titulo, c.Tipo.ToString(), c.Totalizar)).ToList(),
            dados.Linhas.Take(LimiteDaTela).ToList(), dados.Filtros, dados.Linhas.Count));
    }

    private static async Task<IResult> Exportar(
        string codigo, string formato, HttpContext http,
        IEnumerable<IRelatorio> relatorios, IEnumerable<IExportadorDeRelatorio> exportadores,
        IEmpresaAtiva empresaAtiva, ITenantContext tenant, IUsuarioAtual usuario, IRegistradorDeEventos eventos,
        FilaDeExportacao fila, IOptions<RelatoriosOptions> opcoes, TimeProvider relogio, CancellationToken ct)
    {
        var relatorio = relatorios.FirstOrDefault(r => r.Codigo == codigo);
        var exportador = exportadores.FirstOrDefault(e => string.Equals(e.Formato, formato, StringComparison.OrdinalIgnoreCase));
        if (exportador is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Relatório não encontrado",
                detail: "Relatório ou formato inexistente. Formatos disponíveis: pdf, xlsx e csv.");
        if (Barrar(relatorio, empresaAtiva) is { } barrado) return barrado;

        var dados = await relatorio!.GerarAsync(Parametros(http), ct);
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
