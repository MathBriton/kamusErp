using System.Security.Claims;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Planilhas;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kamus.Infrastructure.Importacao;

/// <summary>Importação em 4 passos: modelo → enviar e validar (simulação, nada é gravado) → confirmar → resultado.</summary>
public static partial class ImportacaoEndpoints
{
    private const long TamanhoMaximo = 5 * 1024 * 1024;
    private const int LimiteSincronoPadrao = 500;

    public sealed record ValidacaoDto(Guid Id, string Tipo, string NomeDoArquivo, int Total, int Validas, int ComErro, int Criar, int Atualizar, IReadOnlyList<LinhaValidada> Linhas);
    public sealed record EmSegundoPlanoDto(string Mensagem);

    public static IServiceCollection AddKamusImportacao(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SessoesDeImportacao>();
        return services;
    }

    public static IEndpointRouteBuilder MapKamusImportacao(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/importacoes").RequireAuthorization().WithTags("Importação por planilha");
        g.MapGet("/{tipo}/modelo.xlsx", Modelo).WithSummary("Modelo .xlsx do tipo (clientes, fornecedores, produtos, estoque-inicial...) com instruções");
        g.MapPost("/{tipo}/validar", Validar).DisableAntiforgery()
            .WithSummary("Envia a planilha (.xlsx ou .csv, campo 'arquivo') e devolve a prévia linha a linha. Não grava nada.")
            .Produces<ValidacaoDto>().ProducesProblem(StatusCodes.Status400BadRequest);
        g.MapPost("/{id:guid}/confirmar", Confirmar)
            .WithSummary("Importa as linhas válidas. Acima de Importacao:LimiteSincrono, roda em segundo plano e avisa no sininho")
            .Produces<ResultadoDaImportacao>().Produces<EmSegundoPlanoDto>(StatusCodes.Status202Accepted);
        return app;
    }

    private static IImportador? Importador(IEnumerable<IImportador> importadores, string tipo) =>
        importadores.FirstOrDefault(i => string.Equals(i.Tipo, tipo, StringComparison.OrdinalIgnoreCase));

    private static IResult Problema(int status, string detalhe) => Results.Problem(statusCode: status, title: "Importação", detail: detalhe);

    private static IResult? Barrar(IImportador? importador, IEmpresaAtiva empresa) =>
        importador is null ? Problema(StatusCodes.Status404NotFound, "Tipo de importação desconhecido.")
        : !importador.Perfis.Contains(empresa.Perfil) ? Problema(StatusCodes.Status403Forbidden, $"Seu perfil não importa {importador.Titulo.ToLowerInvariant()}.")
        : null;

    private static Guid UsuarioId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    private static IResult Modelo(string tipo, IEnumerable<IImportador> importadores, IEmpresaAtiva empresa)
    {
        var importador = Importador(importadores, tipo);
        if (Barrar(importador, empresa) is { } barrado) return barrado;
        return Results.File(ModeloDePlanilha.Gerar(importador!.Titulo, importador.Colunas),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"modelo-{importador.Tipo}.xlsx");
    }

    private static async Task<IResult> Validar(string tipo, IFormFile? arquivo, IEnumerable<IImportador> importadores, IEmpresaAtiva empresa,
        ITenantContext tenant, SessoesDeImportacao sessoes, ClaimsPrincipal user, CancellationToken ct)
    {
        var importador = Importador(importadores, tipo);
        if (Barrar(importador, empresa) is { } barrado) return barrado;
        if (arquivo is null || arquivo.Length == 0) return Problema(StatusCodes.Status400BadRequest, "Escolha a planilha (.xlsx ou .csv).");
        if (arquivo.Length > TamanhoMaximo) return Problema(StatusCodes.Status400BadRequest, "A planilha passa de 5 MB. Divida em arquivos menores.");

        TabelaLida tabela;
        try
        {
            await using var conteudo = arquivo.OpenReadStream();
            using var copia = new MemoryStream();
            await conteudo.CopyToAsync(copia, ct);
            copia.Position = 0;
            tabela = LeitorDePlanilha.Ler(copia, arquivo.FileName);
        }
        catch (PlanilhaInvalidaException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message);
        }

        var contexto = new ContextoDaImportacao(UsuarioId(user), user.FindFirstValue("name"), empresa.EmpresaId, empresa.Nome);
        var linhas = await importador!.ValidarAsync(tabela, contexto, ct);
        var sessao = sessoes.Guardar(importador.Tipo, tenant.TenantId!.Value, contexto, arquivo.FileName, tabela);
        return Results.Ok(new ValidacaoDto(sessao.Id, importador.Tipo, arquivo.FileName, linhas.Count,
            linhas.Count(l => l.Acao != AcaoDaLinha.Erro), linhas.Count(l => l.Acao == AcaoDaLinha.Erro),
            linhas.Count(l => l.Acao == AcaoDaLinha.Criar), linhas.Count(l => l.Acao == AcaoDaLinha.Atualizar), linhas));
    }

    private static async Task<IResult> Confirmar(Guid id, IEnumerable<IImportador> importadores, IEmpresaAtiva empresa, ITenantContext tenant,
        SessoesDeImportacao sessoes, ClaimsPrincipal user, IConfiguration configuracao, IServiceScopeFactory escopos, CancellationToken ct)
    {
        var usuarioId = UsuarioId(user);
        var sessao = sessoes.Retirar(id, tenant.TenantId!.Value, usuarioId);
        if (sessao is null) return Problema(StatusCodes.Status404NotFound, "Importação não encontrada ou vencida (30 minutos). Envie a planilha de novo.");
        var importador = Importador(importadores, sessao.Tipo);
        if (Barrar(importador, empresa) is { } barrado) return barrado;
        if (sessao.Contexto.EmpresaId != empresa.EmpresaId)
            return Problema(StatusCodes.Status409Conflict, $"A planilha foi validada na empresa {sessao.Contexto.EmpresaNome}. Volte para ela ou envie a planilha de novo.");

        var validas = sessao.Tabela.Linhas.Count;
        if (validas <= configuracao.GetValue("Importacao:LimiteSincrono", LimiteSincronoPadrao))
            return Results.Ok(await importador!.ImportarAsync(sessao.Tabela, sessao.Contexto, ct));

        _ = Task.Run(() => ImportarEmSegundoPlanoAsync(escopos, sessao), CancellationToken.None);
        return Results.Accepted(value: new EmSegundoPlanoDto(
            $"A planilha tem {validas} linhas: a importação continua em segundo plano e você será avisado no sininho."));
    }

    private static async Task ImportarEmSegundoPlanoAsync(IServiceScopeFactory escopos, SessoesDeImportacao.Sessao sessao)
    {
        var (usuarioId, empresaId) = (sessao.Contexto.UsuarioId, sessao.Contexto.EmpresaId);
        await using var escopo = escopos.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        sp.GetRequiredService<TenantContext>().Definir(sessao.TenantId);
        var notificador = sp.GetRequiredService<INotificador>();
        var importador = sp.GetServices<IImportador>().Single(i => i.Tipo == sessao.Tipo);
        try
        {
            var r = await importador.ImportarAsync(sessao.Tabela, sessao.Contexto, CancellationToken.None);
            await notificador.NotificarAsync(new NovaNotificacao(usuarioId, "importacao_concluida",
                $"Importação de {importador.Titulo.ToLowerInvariant()} concluída: {r.Criados} criados, {r.Atualizados} atualizados",
                $"{sessao.NomeDoArquivo}: {r.Ignorados} linhas com erro foram ignoradas.", importador.Link, empresaId));
        }
        catch (Exception ex)
        {
            FalhaNaImportacao(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kamus.Importacao"), ex, sessao.Tipo, sessao.TenantId);
            await notificador.NotificarAsync(new NovaNotificacao(usuarioId, "importacao_falhou", $"A importação de {importador.Titulo.ToLowerInvariant()} falhou",
                $"{sessao.NomeDoArquivo}: nada foi gravado. Tente de novo ou fale com o suporte.", importador.Link, empresaId));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha na importação em segundo plano ({Tipo}) do cliente {TenantId}")]
    private static partial void FalhaNaImportacao(ILogger logger, Exception ex, string tipo, Guid tenantId);
}
