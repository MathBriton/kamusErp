using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Infrastructure.MultiTenancy;

/// <summary>Implementação com escopo por requisição. Será preenchida a partir do token do usuário.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Definir(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant inválido.", nameof(tenantId));
        if (TenantId is { } atual && atual != tenantId)
            throw new InvalidOperationException("O tenant desta operação já foi definido e não pode ser trocado.");

        TenantId = tenantId;
    }
}
