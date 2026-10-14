using System.ComponentModel;
using Kamus.SharedKernel.Auditoria;

namespace Kamus.Modules.Licenciamento.Dominio;

/// <summary>Registro (somente inclusão) do que cada operador fez no Painel Administrativo.</summary>
[DisplayName("Ação administrativa")]
[NaoAuditar]
public sealed class AcaoAdministrativa
{
    public Guid Id { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }
    public Guid? AdministradorId { get; private set; }
    public string AdministradorEmail { get; private set; } = "";
    public string Acao { get; private set; } = "";
    public Guid? TenantId { get; private set; }
    public string Descricao { get; private set; } = "";
    public string? Ip { get; private set; }

    private AcaoAdministrativa() { }

    public static AcaoAdministrativa Criar(Guid? administradorId, string administradorEmail, string acao, Guid? tenantId, string descricao, string? ip, DateTimeOffset agora) =>
        new()
        {
            Id = Guid.NewGuid(),
            OcorridoEm = agora,
            AdministradorId = administradorId,
            AdministradorEmail = administradorEmail,
            Acao = acao,
            TenantId = tenantId,
            Descricao = descricao,
            Ip = ip,
        };
}
