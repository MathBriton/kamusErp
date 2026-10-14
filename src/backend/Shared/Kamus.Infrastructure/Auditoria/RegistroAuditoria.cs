using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Infrastructure.Auditoria;

public enum TipoRegistroAuditoria
{
    Inclusao = 1,
    Alteracao = 2,
    Exclusao = 3,
    Evento = 4,
}

public sealed record AlteracaoDeCampo(string Campo, string? Antes, string? Depois);

/// <summary>Linha do histórico. Somente inclusão: nunca é alterada nem apagada.</summary>
public sealed class RegistroAuditoria : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? EmpresaId { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }
    public TipoRegistroAuditoria Tipo { get; private set; }
    public string? Evento { get; private set; }
    public string? Entidade { get; private set; }
    public string? EntidadeId { get; private set; }
    public string Descricao { get; private set; } = "";
    public Guid? UsuarioId { get; private set; }
    public string UsuarioNome { get; private set; } = "";
    public string? Ip { get; private set; }
    public List<AlteracaoDeCampo> Alteracoes { get; private set; } = [];

    private RegistroAuditoria() { }

    public static RegistroAuditoria DeAlteracao(TipoRegistroAuditoria tipo, string entidade, string entidadeId,
        List<AlteracaoDeCampo> alteracoes, Autoria autoria) =>
        new RegistroAuditoria
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Entidade = entidade,
            EntidadeId = entidadeId,
            Descricao = $"{NomeDoTipo(tipo)} de {entidade}",
            Alteracoes = alteracoes,
        }.Com(autoria);

    public static RegistroAuditoria DeEvento(string evento, string descricao, Autoria autoria) =>
        new RegistroAuditoria { Id = Guid.NewGuid(), Tipo = TipoRegistroAuditoria.Evento, Evento = evento, Descricao = descricao }.Com(autoria);

    private RegistroAuditoria Com(Autoria autoria)
    {
        OcorridoEm = autoria.Em;
        EmpresaId = autoria.EmpresaId;
        UsuarioId = autoria.UsuarioId;
        UsuarioNome = string.IsNullOrWhiteSpace(autoria.UsuarioNome) ? "Sistema" : autoria.UsuarioNome;
        Ip = autoria.Ip;
        return this;
    }

    private static string NomeDoTipo(TipoRegistroAuditoria tipo) => tipo switch
    {
        TipoRegistroAuditoria.Inclusao => "Inclusão",
        TipoRegistroAuditoria.Alteracao => "Alteração",
        TipoRegistroAuditoria.Exclusao => "Exclusão",
        _ => "Evento",
    };
}

/// <summary>Quem, quando, de onde e em qual empresa.</summary>
public sealed record Autoria(DateTimeOffset Em, Guid? UsuarioId, string? UsuarioNome, string? Ip, Guid? EmpresaId);
