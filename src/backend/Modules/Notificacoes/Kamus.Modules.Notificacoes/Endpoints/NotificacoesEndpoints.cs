using System.Security.Claims;
using Kamus.Modules.Notificacoes.Aplicacao;
using Kamus.Modules.Notificacoes.Persistencia;
using Kamus.SharedKernel.Notificacoes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Notificacoes.Endpoints;

internal static class NotificacoesEndpoints
{
    public sealed record ListaResponse(IReadOnlyList<NotificacaoDto> Itens, int NaoLidas);

    /// <param name="Plataforma">android, ios ou web.</param>
    public sealed record DispositivoRequest(string? Token, string? Plataforma);

    public static IEndpointRouteBuilder MapNotificacoes(this IEndpointRouteBuilder app, bool incluirEndpointDeTeste)
    {
        var grupo = app.MapGroup("/api/notificacoes").RequireAuthorization().WithTags("Notificações");

        grupo.MapGet("", Listar).WithSummary("Notificações do usuário logado (mais recentes primeiro)").Produces<ListaResponse>();
        grupo.MapPost("/{id:guid}/lida", MarcarComoLida).WithSummary("Marcar uma notificação como lida")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        grupo.MapPost("/lidas", MarcarTodasComoLidas).WithSummary("Marcar todas como lidas").Produces(StatusCodes.Status204NoContent);

        grupo.MapPut("/dispositivos", RegistrarDispositivo).WithSummary("[App mobile] Registra este aparelho para receber as notificações por push (token do Expo)")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status400BadRequest);
        grupo.MapDelete("/dispositivos", RemoverDispositivo).WithSummary("[App mobile] Para de enviar push para este aparelho (ao sair do app)")
            .Produces(StatusCodes.Status204NoContent);

        if (incluirEndpointDeTeste)
            grupo.MapPost("/teste", EnviarTeste).WithSummary("[Desenvolvimento] Envia uma notificação de teste para você").Produces(StatusCodes.Status202Accepted);

        return app;
    }

    private static Guid UsuarioId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    private static async Task<IResult> Listar(ClaimsPrincipal user, NotificacoesDbContext db, CancellationToken ct, bool somenteNaoLidas = false, int limite = 30)
    {
        var usuarioId = UsuarioId(user);
        var doUsuario = db.Notificacoes.AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        var naoLidas = await doUsuario.CountAsync(n => n.LidaEm == null, ct);

        var consulta = somenteNaoLidas ? doUsuario.Where(n => n.LidaEm == null) : doUsuario;
        var itens = await consulta.OrderByDescending(n => n.CriadaEm).Take(Math.Clamp(limite, 1, 100)).ToListAsync(ct);
        return Results.Ok(new ListaResponse(itens.Select(NotificacaoDto.De).ToList(), naoLidas));
    }

    private static async Task<IResult> MarcarComoLida(Guid id, ClaimsPrincipal user, NotificacoesDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var usuarioId = UsuarioId(user);
        var notificacao = await db.Notificacoes.SingleOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId, ct);
        if (notificacao is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Notificação não encontrada", detail: "Notificação não encontrada.");

        notificacao.MarcarComoLida(relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MarcarTodasComoLidas(ClaimsPrincipal user, NotificacoesDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var usuarioId = UsuarioId(user);
        var agora = relogio.GetUtcNow();
        var naoLidas = await db.Notificacoes.Where(n => n.UsuarioId == usuarioId && n.LidaEm == null).ToListAsync(ct);
        foreach (var n in naoLidas) n.MarcarComoLida(agora);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RegistrarDispositivo(DispositivoRequest r, ClaimsPrincipal user, NotificacoesDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        if (!Dominio.Dispositivo.TokenValido(r.Token))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Aparelho", detail: "Token de push inválido.");
        var usuarioId = UsuarioId(user);
        var existente = await db.Dispositivos.SingleOrDefaultAsync(d => d.Token == r.Token, ct);
        if (existente is null) db.Dispositivos.Add(Dominio.Dispositivo.Registrar(usuarioId, r.Token!, r.Plataforma, relogio.GetUtcNow()));
        else existente.Transferir(usuarioId, r.Plataforma, relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoverDispositivo([FromBody] DispositivoRequest r, ClaimsPrincipal user, NotificacoesDbContext db, CancellationToken ct)
    {
        var usuarioId = UsuarioId(user);
        var dispositivo = await db.Dispositivos.SingleOrDefaultAsync(d => d.Token == r.Token && d.UsuarioId == usuarioId, ct);
        if (dispositivo is not null)
        {
            db.Dispositivos.Remove(dispositivo);
            await db.SaveChangesAsync(ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> EnviarTeste(ClaimsPrincipal user, INotificador notificador, CancellationToken ct)
    {
        await notificador.NotificarAsync(new NovaNotificacao(UsuarioId(user), "teste", "Notificação de teste",
            $"Enviada às {DateTimeOffset.Now:HH:mm:ss}. Se você está vendo isto sem recarregar a página, o tempo real está funcionando.", "/"), ct);
        return Results.Accepted();
    }
}
