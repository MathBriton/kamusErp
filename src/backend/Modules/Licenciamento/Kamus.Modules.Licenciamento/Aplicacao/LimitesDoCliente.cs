using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.Clientes;

namespace Kamus.Modules.Licenciamento.Aplicacao;

internal sealed class LimitesDaLicenca(CacheDeLicencas cache, LicenciamentoDbContext db) : ILimitesDoCliente
{
    public async Task<LimitesDoCliente> ObterAsync(Guid tenantId, CancellationToken ct = default) =>
        await cache.ObterAsync(tenantId, db, ct) is { } l
            ? new LimitesDoCliente(l.LimiteDeCnpjs, l.LimiteDeLojas, l.LimiteDeUsuarios)
            : LimitesDoCliente.SemLimites;
}
