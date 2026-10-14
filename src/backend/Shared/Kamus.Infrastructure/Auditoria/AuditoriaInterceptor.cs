using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Infrastructure.Auditoria;

/// <summary>
/// Captura inclusões, alterações e exclusões de entidades de negócio no <c>SaveChanges</c> de qualquer
/// DbContext de módulo e grava no histórico logo depois que a gravação dá certo.
/// Auditadas: entidades <see cref="ITenantOwned"/> e as marcadas com <see cref="AuditavelAttribute"/>.
/// Ignoradas: classes/campos com <see cref="NaoAuditarAttribute"/> e o próprio <c>TenantId</c>.
/// </summary>
internal sealed class AuditoriaInterceptor(IServiceProvider servicos) : SaveChangesInterceptor
{
    private sealed record Pendente(TipoRegistroAuditoria Tipo, string Entidade, string NomeExibicao, string EntidadeId, List<AlteracaoDeCampo> Alteracoes);

    private List<Pendente> _pendentes = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capturar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capturar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var db = Preparar();
        db?.SaveChanges();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var db = Preparar();
        if (db is not null) await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pendentes = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendentes = [];
        return Task.CompletedTask;
    }

    private void Capturar(DbContext? contexto)
    {
        _pendentes = [];
        if (contexto is null) return;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            var tipoClr = entrada.Metadata.ClrType;
            if (!DeveAuditar(tipoClr)) continue;

            var tipo = entrada.State switch
            {
                EntityState.Added => TipoRegistroAuditoria.Inclusao,
                EntityState.Deleted => TipoRegistroAuditoria.Exclusao,
                _ => TipoRegistroAuditoria.Alteracao,
            };

            var alteracoes = CamposAlterados(entrada, tipo);
            if (tipo == TipoRegistroAuditoria.Alteracao && alteracoes.Count == 0) continue;

            var nome = tipoClr.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? tipoClr.Name;
            _pendentes.Add(new Pendente(tipo, tipoClr.Name, nome, Chave(entrada), alteracoes));
        }
    }

    private AuditoriaDbContext? Preparar()
    {
        var pendentes = _pendentes;
        _pendentes = [];
        // Sem tenant não há a quem atribuir o registro (o RLS também não permitiria gravá-lo).
        if (pendentes.Count == 0 || servicos.GetRequiredService<ITenantContext>().TenantId is null) return null;

        var autoria = servicos.GetRequiredService<FonteDeAutoria>().Atual();
        var db = servicos.GetRequiredService<AuditoriaDbContext>();
        foreach (var p in pendentes)
            db.Registros.Add(RegistroAuditoria.DeAlteracao(p.Tipo, p.Entidade, p.NomeExibicao, p.EntidadeId, p.Alteracoes, autoria));
        return db;
    }

    private static bool DeveAuditar(Type tipo) =>
        tipo != typeof(RegistroAuditoria)
        && tipo.GetCustomAttribute<NaoAuditarAttribute>() is null
        && (typeof(ITenantOwned).IsAssignableFrom(tipo) || tipo.GetCustomAttribute<AuditavelAttribute>() is not null);

    private static List<AlteracaoDeCampo> CamposAlterados(EntityEntry entrada, TipoRegistroAuditoria tipo)
    {
        var lista = new List<AlteracaoDeCampo>();
        foreach (var prop in entrada.Properties)
        {
            var info = prop.Metadata.PropertyInfo;
            if (info is null || prop.Metadata.Name == nameof(ITenantOwned.TenantId) || info.GetCustomAttribute<NaoAuditarAttribute>() is not null)
                continue;

            switch (tipo)
            {
                case TipoRegistroAuditoria.Inclusao:
                    lista.Add(new AlteracaoDeCampo(prop.Metadata.Name, null, Formatar(prop.CurrentValue)));
                    break;
                case TipoRegistroAuditoria.Exclusao:
                    lista.Add(new AlteracaoDeCampo(prop.Metadata.Name, Formatar(prop.OriginalValue), null));
                    break;
                default:
                    var antes = Formatar(prop.OriginalValue);
                    var depois = Formatar(prop.CurrentValue);
                    if (prop.IsModified && antes != depois)
                        lista.Add(new AlteracaoDeCampo(prop.Metadata.Name, antes, depois));
                    break;
            }
        }
        return lista;
    }

    private static string Chave(EntityEntry entrada) =>
        string.Join("|", entrada.Metadata.FindPrimaryKey()!.Properties.Select(p => Formatar(entrada.Property(p.Name).CurrentValue)));

    private static string? Formatar(object? valor) => valor switch
    {
        null => null,
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString(),
    };
}
