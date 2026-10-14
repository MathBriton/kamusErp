using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Kamus.Infrastructure.Auditoria;

/// <summary>Usuário vindo do token (claims <c>sub</c> e <c>name</c>) e IP da requisição.</summary>
internal sealed class UsuarioAtualHttp(IHttpContextAccessor acessor) : IUsuarioAtual
{
    private HttpContext? Http => acessor.HttpContext;

    public Guid? Id => Guid.TryParse(Http?.User.FindFirst("sub")?.Value, out var id) ? id : null;
    public string? Nome => Http?.User.FindFirst("name")?.Value;
    public string? Ip => Http?.Connection.RemoteIpAddress?.ToString();
}

internal sealed class FonteDeAutoria(IUsuarioAtual usuario, IEmpresaAtiva empresa, TimeProvider relogio)
{
    public Autoria Atual(Guid? usuarioId = null, string? usuarioNome = null) =>
        new(relogio.GetUtcNow(),
            usuarioId ?? usuario.Id,
            usuarioNome ?? usuario.Nome,
            usuario.Ip,
            empresa.Definida && !empresa.Consolidado ? empresa.EmpresaId : null);
}

internal sealed class RegistradorDeEventos(AuditoriaDbContext db, FonteDeAutoria autoria, ITenantContext tenant) : IRegistradorDeEventos
{
    public async Task RegistrarAsync(string evento, string descricao, Guid? usuarioId = null, string? usuarioNome = null, CancellationToken ct = default)
    {
        if (tenant.TenantId is null) return; // sem tenant não há a quem atribuir o evento
        db.Registros.Add(RegistroAuditoria.DeEvento(evento, descricao, autoria.Atual(usuarioId, usuarioNome)));
        await db.SaveChangesAsync(ct);
    }
}
