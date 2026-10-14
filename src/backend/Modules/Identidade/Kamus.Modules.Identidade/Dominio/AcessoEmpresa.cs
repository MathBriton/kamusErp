using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Identidade.Dominio;

/// <summary>Quais empresas o usuário acessa e com qual perfil.</summary>
[DisplayName("Acesso à empresa")]
public sealed class AcessoEmpresa : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Perfil Perfil { get; private set; }

    private AcessoEmpresa() { }

    public static AcessoEmpresa Criar(Guid usuarioId, Guid empresaId, Perfil perfil) =>
        new() { Id = Guid.NewGuid(), UsuarioId = usuarioId, EmpresaId = empresaId, Perfil = perfil };
}
