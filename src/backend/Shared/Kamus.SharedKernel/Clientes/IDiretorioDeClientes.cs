namespace Kamus.SharedKernel.Clientes;

/// <summary>Cliente do SaaS (tenant) visto de fora do módulo Identidade (ex.: licenciamento, Painel Administrativo).</summary>
public sealed record UsuarioDoCliente(Guid Id, string Nome, string Email, bool Ativo);

/// <param name="Perfil">Dono, Gerente, Vendedor, Estoquista ou Financeiro.</param>
public sealed record AcessoDoUsuario(Guid UsuarioId, Guid EmpresaId, string Empresa, string Perfil);

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

    /// <summary>Empresas e perfis de cada usuário <b>ativo</b> do tenant atual (ex.: resumo diário).</summary>
    Task<IReadOnlyList<AcessoDoUsuario>> AcessosDoTenantAtualAsync(CancellationToken ct = default);
}
