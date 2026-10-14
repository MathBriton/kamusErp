using System.Collections.Concurrent;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>
/// Evita ler a licença no banco a cada requisição. Dura 1 minuto; o Painel Administrativo invalida na hora ao
/// registrar pagamento, cancelar ou reativar (mesmo processo). O estado é recalculado pela data a cada uso.
/// </summary>
public sealed class CacheDeLicencas(TimeProvider relogio)
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, (Licenca? Licenca, DateTimeOffset Expira)> _itens = new();

    public async Task<Licenca?> ObterAsync(Guid tenantId, LicenciamentoDbContext db, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        if (_itens.TryGetValue(tenantId, out var item) && item.Expira > agora) return item.Licenca;

        var licenca = await db.Licencas.AsNoTracking().SingleOrDefaultAsync(l => l.TenantId == tenantId, ct);
        _itens[tenantId] = (licenca, agora + Validade);
        return licenca;
    }

    public void Invalidar(Guid tenantId) => _itens.TryRemove(tenantId, out _);
}
