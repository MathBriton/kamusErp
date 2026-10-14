using System.Globalization;

namespace Kamus.Infrastructure.Auditoria;

/// <summary>Filtros do histórico, usados igualmente pela tela (/api/auditoria) e pela exportação.</summary>
public sealed record FiltrosDoHistorico(
    string? Entidade = null, string? EntidadeId = null, TipoRegistroAuditoria? Tipo = null, string? Evento = null,
    DateTimeOffset? De = null, DateTimeOffset? Ate = null)
{
    public static FiltrosDoHistorico DosParametros(IReadOnlyDictionary<string, string?> p) => new(
        Valor(p, "entidade"),
        Valor(p, "entidadeId"),
        Enum.TryParse<TipoRegistroAuditoria>(Valor(p, "tipo"), ignoreCase: true, out var tipo) ? tipo : null,
        Valor(p, "evento"),
        DateTimeOffset.TryParse(Valor(p, "de"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var de) ? de : null,
        DateTimeOffset.TryParse(Valor(p, "ate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ate) ? ate : null);

    private static string? Valor(IReadOnlyDictionary<string, string?> p, string chave) =>
        p.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    /// <summary>Registros da(s) empresa(s) ativa(s) + registros gerais do cliente (sem empresa: logins, usuários...).</summary>
    public IQueryable<RegistroAuditoria> Aplicar(IQueryable<RegistroAuditoria> consulta, IReadOnlyList<Guid> empresas)
    {
        consulta = consulta.Where(r => r.EmpresaId == null || empresas.Contains(r.EmpresaId.Value));
        if (Entidade is not null) consulta = consulta.Where(r => r.Entidade == Entidade);
        if (EntidadeId is not null) consulta = consulta.Where(r => r.EntidadeId == EntidadeId);
        if (Tipo is not null) consulta = consulta.Where(r => r.Tipo == Tipo);
        if (Evento is not null) consulta = consulta.Where(r => r.Evento == Evento);
        if (De is not null) consulta = consulta.Where(r => r.OcorridoEm >= De);
        if (Ate is not null) consulta = consulta.Where(r => r.OcorridoEm <= Ate);
        return consulta.OrderByDescending(r => r.OcorridoEm);
    }

    public static string NomeDoTipo(TipoRegistroAuditoria tipo) => tipo switch
    {
        TipoRegistroAuditoria.Inclusao => "Inclusão",
        TipoRegistroAuditoria.Alteracao => "Alteração",
        TipoRegistroAuditoria.Exclusao => "Exclusão",
        _ => "Evento",
    };
}
