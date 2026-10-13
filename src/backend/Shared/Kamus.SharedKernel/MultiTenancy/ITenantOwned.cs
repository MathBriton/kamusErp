namespace Kamus.SharedKernel.MultiTenancy;

/// <summary>
/// Entidade que pertence a um tenant (cliente do SaaS). O <see cref="TenantId"/> é
/// preenchido pela infraestrutura ao salvar e nunca pode ser alterado depois.
/// Toda entidade de negócio deve implementar esta interface.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
