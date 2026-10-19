using Kamus.Modules.Indicadores.Aplicacao;
using Kamus.Modules.Indicadores.Dominio;
using Kamus.Modules.Indicadores.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Notificacoes;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Indicadores.Endpoints;

public static class IndicadoresEndpoints
{
    /// <param name="Dias">0 = domingo … 6 = sábado.</param>
    public sealed record PreferenciasDto(bool Ativo, int Hora, IReadOnlyList<int> Dias, bool Vendas, bool Financeiro, bool Estoque, bool Condicionais, bool Crediario,
        bool EnviarSemMovimento, DateOnly? UltimoEnvio);

    public static IEndpointRouteBuilder MapIndicadores(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/indicadores").RequireAuthorization().WithTags("Indicadores");
        g.MapGet("/", Dashboard).WithSummary("Dashboard da empresa ativa (ou consolidado): vendas com comparação, financeiro, estoque, condicionais e alertas. "
            + "periodo: hoje, 7dias ou mes. Cada perfil vê só os blocos dele (o Vendedor, as próprias vendas)").Produces<Aplicacao.Dashboard>();
        g.MapGet("/resumo-diario", Preferencias).WithSummary("Preferências do resumo diário do usuário logado (padrão: ligado às 20h para o Dono)")
            .Produces<PreferenciasDto>();
        g.MapPut("/resumo-diario", Salvar).WithSummary("Altera horário (0–23, Brasília), dias da semana e blocos do resumo diário").Produces<PreferenciasDto>();
        g.MapGet("/resumo-diario/previa", Previa).WithSummary("Como ficaria o resumo de hoje para o usuário logado").Produces<PreviaDoResumo>();
        g.MapPost("/resumo-diario/enviar", Enviar).WithSummary("Envia agora o resumo de hoje para o sininho do usuário logado (teste)");
        return app;
    }

    private static IResult Problema(int status, string detalhe) => Results.Problem(statusCode: status, title: "Indicadores", detail: detalhe);

    private static async Task<IResult> Dashboard(string? periodo, IEmpresaAtiva empresa, IUsuarioAtual usuario, Painel painel, TimeProvider relogio, CancellationToken ct)
    {
        if (!empresa.Definida || empresa.Perfil is null) return Problema(StatusCodes.Status400BadRequest, "Escolha uma empresa para ver o dashboard.");
        var hoje = CalendarioBrasilia.Hoje(relogio);
        if (Periodo.Escolher(periodo, hoje) is not { } p) return Problema(StatusCodes.Status400BadRequest, "Período inválido: use hoje, 7dias ou mes.");
        return Results.Ok(await painel.MontarAsync(empresa.EmpresaIds, empresa.Perfil, usuario.Id, p, hoje, ct));
    }

    private static PreferenciasDto Dto(PreferenciasDoResumo p) =>
        new(p.Ativo, p.Hora, p.DiasDaSemana(), p.Vendas, p.Financeiro, p.Estoque, p.Condicionais, p.Crediario, p.EnviarSemMovimento, p.UltimoEnvio);

    private static async Task<(PreferenciasDoResumo Preferencias, IReadOnlyList<AcessoDoUsuario> Acessos, bool Nova)> CarregarAsync(Guid usuarioId,
        IndicadoresDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        var acessos = (await diretorio.AcessosDoTenantAtualAsync(ct)).Where(a => a.UsuarioId == usuarioId).ToList();
        var salva = await db.Preferencias.SingleOrDefaultAsync(p => p.UsuarioId == usuarioId, ct);
        return salva is not null ? (salva, acessos, false) : (PreferenciasDoResumo.Padrao(usuarioId, acessos.Any(a => a.Perfil == "Dono")), acessos, true);
    }

    private static async Task<IResult> Preferencias(IUsuarioAtual usuario, IndicadoresDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (usuario.Id is not { } id) return Results.Unauthorized();
        return Results.Ok(Dto((await CarregarAsync(id, db, diretorio, ct)).Preferencias));
    }

    private static async Task<IResult> Salvar(PreferenciasDto r, IUsuarioAtual usuario, IndicadoresDbContext db, IDiretorioDeClientes diretorio, CancellationToken ct)
    {
        if (usuario.Id is not { } id) return Results.Unauthorized();
        var (p, _, nova) = await CarregarAsync(id, db, diretorio, ct);
        try
        {
            p.Alterar(r.Ativo, r.Hora, r.Dias ?? [], r.Vendas, r.Financeiro, r.Estoque, r.Condicionais, r.Crediario, r.EnviarSemMovimento);
        }
        catch (ArgumentException ex)
        {
            return Problema(StatusCodes.Status400BadRequest, ex.Message.Split(" (Parameter")[0]);
        }
        if (nova) db.Preferencias.Add(p);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(p));
    }

    private static async Task<PreviaDoResumo> MontarAsync(Guid id, IndicadoresDbContext db, IDiretorioDeClientes diretorio, ResumoDiario resumo, TimeProvider relogio,
        CancellationToken ct)
    {
        var (p, acessos, _) = await CarregarAsync(id, db, diretorio, ct);
        return await resumo.MontarAsync(id, acessos, p, CalendarioBrasilia.Hoje(relogio), ct);
    }

    private static async Task<IResult> Previa(IUsuarioAtual usuario, IndicadoresDbContext db, IDiretorioDeClientes diretorio, ResumoDiario resumo, TimeProvider relogio,
        CancellationToken ct) =>
        usuario.Id is { } id ? Results.Ok(await MontarAsync(id, db, diretorio, resumo, relogio, ct)) : Results.Unauthorized();

    private static async Task<IResult> Enviar(IUsuarioAtual usuario, IndicadoresDbContext db, IDiretorioDeClientes diretorio, ResumoDiario resumo, INotificador notificador,
        TimeProvider relogio, CancellationToken ct)
    {
        if (usuario.Id is not { } id) return Results.Unauthorized();
        var previa = await MontarAsync(id, db, diretorio, resumo, relogio, ct);
        await notificador.NotificarAsync(new NovaNotificacao(id, GeradorDeResumos.Tipo, previa.Titulo, previa.Mensagem(), "/"), ct);
        return Results.NoContent();
    }
}
