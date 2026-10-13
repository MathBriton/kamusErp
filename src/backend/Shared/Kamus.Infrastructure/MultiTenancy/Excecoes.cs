namespace Kamus.Infrastructure.MultiTenancy;

public sealed class TenantNaoDefinidoException()
    : InvalidOperationException("Operação de escrita sem tenant definido.");

public sealed class EscritaEntreTenantsException(string entidade)
    : InvalidOperationException($"Tentativa de gravar '{entidade}' pertencente a outro tenant.");
