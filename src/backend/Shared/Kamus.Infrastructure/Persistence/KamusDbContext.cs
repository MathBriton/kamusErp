using System.Linq.Expressions;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Infrastructure.Persistence;

/// <summary>
/// Base de todos os DbContexts dos módulos. Aplica a primeira camada de isolamento entre tenants:
/// filtro global nas consultas e validação na gravação. Sem tenant definido, nada é lido nem gravado.
/// </summary>
public abstract class KamusDbContext(DbContextOptions options, ITenantContext tenant) : DbContext(options)
{
    // Lido pelo filtro global a cada consulta (o EF parametriza membros do próprio DbContext).
    private Guid? TenantAtual => tenant.TenantId;

    private static readonly System.Reflection.PropertyInfo TenantAtualProperty =
        typeof(KamusDbContext).GetProperty(nameof(TenantAtual), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;

            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(ITenantOwned.TenantId)).IsRequired();
            builder.HasIndex(nameof(ITenantOwned.TenantId));
            builder.HasQueryFilter(CriarFiltroTenant(entityType.ClrType));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarRegrasDeTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarRegrasDeTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AplicarRegrasDeTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var tenantAtual = TenantAtual ?? throw new TenantNaoDefinidoException();
            var propriedade = entry.Property(e => e.TenantId);
            var nomeEntidade = entry.Metadata.ClrType.Name;

            if (entry.State == EntityState.Added)
            {
                if (propriedade.CurrentValue != Guid.Empty && propriedade.CurrentValue != tenantAtual)
                    throw new EscritaEntreTenantsException(nomeEntidade);
                propriedade.CurrentValue = tenantAtual;
                continue;
            }

            // Modified/Deleted: o registro precisa ser do tenant atual e o TenantId não pode mudar.
            if (propriedade.OriginalValue != tenantAtual || propriedade.CurrentValue != tenantAtual)
                throw new EscritaEntreTenantsException(nomeEntidade);
        }
    }

    private LambdaExpression CriarFiltroTenant(Type entidade)
    {
        // e => this.TenantAtual != null && e.TenantId == this.TenantAtual
        var e = Expression.Parameter(entidade, "e");
        var atual = Expression.Property(Expression.Constant(this, typeof(KamusDbContext)), TenantAtualProperty);
        var tenantDaEntidade = Expression.Convert(Expression.Property(e, nameof(ITenantOwned.TenantId)), typeof(Guid?));
        var corpo = Expression.AndAlso(
            Expression.NotEqual(atual, Expression.Constant(null, typeof(Guid?))),
            Expression.Equal(tenantDaEntidade, atual));
        return Expression.Lambda(corpo, e);
    }
}
