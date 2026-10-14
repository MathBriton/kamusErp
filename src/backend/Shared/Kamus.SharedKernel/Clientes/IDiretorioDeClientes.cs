namespace Kamus.SharedKernel.Clientes;

/// <summary>Cliente do SaaS (tenant) visto de fora do módulo Identidade (ex.: licenciamento, Painel Administrativo).</summary>
public sealed record UsuarioDoCliente(Guid Id, string Nome, string Email, bool Ativo);

public sealed record ClienteDoSaas(Guid TenantId, string Nome, DateTimeOffset CriadoEm, int Usuarios, string? EmailDoDono);

/// <summary>Consulta os clientes (tenants) e seus usuários. Implementado pelo módulo Identidade.</summary>
public interface IDiretorioDeClientes
{
    /// <summary>Todos os clientes (visão do operador do SaaS, sem filtro de tenant).</summary>
    Task<IReadOnlyList<ClienteDoSaas>> ListarAsync(CancellationToken ct = default);

    /// <summary>Usuários ativos com perfil Dono em alguma empresa do tenant <b>atual</b> (defina o tenant antes).</summary>
    Task<IReadOnlyList<Guid>> DonosDoTenantAtualAsync(CancellationToken ct = default);

    /// <summary>Usuários do tenant atual (ex.: para vincular um vendedor a um login).</summary>
    Task<IReadOnlyList<UsuarioDoCliente>> UsuariosDoTenantAtualAsync(CancellationToken ct = default);
}
