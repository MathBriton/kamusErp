namespace Kamus.SharedKernel.Clientes;

/// <summary>Limites contratados pelo cliente (<c>null</c> = sem limite).</summary>
public sealed record LimitesDoCliente(int? Cnpjs, int? Lojas, int? Usuarios)
{
    public static readonly LimitesDoCliente SemLimites = new(null, null, null);
}

/// <summary>Limites do plano do cliente. Implementado pelo módulo Licenciamento.</summary>
public interface ILimitesDoCliente
{
    Task<LimitesDoCliente> ObterAsync(Guid tenantId, CancellationToken ct = default);
}
