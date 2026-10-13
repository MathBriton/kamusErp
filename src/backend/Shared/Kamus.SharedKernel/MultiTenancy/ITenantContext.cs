namespace Kamus.SharedKernel.MultiTenancy;

/// <summary>Tenant da requisição/operação atual. <c>null</c> quando ainda não identificado.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
