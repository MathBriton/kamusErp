using System.Globalization;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Infrastructure.Auditoria;

/// <summary>O histórico de alterações como relatório exportável (mesmos filtros da tela).</summary>
internal sealed class RelatorioHistoricoDeAlteracoes(AuditoriaDbContext db, IEmpresaAtiva empresaAtiva) : IRelatorio
{
    private const int MaximoDeLinhas = 50_000;

    public string Codigo => "historico-de-alteracoes";
    public string Titulo => "Histórico de alterações";
    public IReadOnlyCollection<string> Perfis => AuditoriaEndpoints.PerfisComAcesso;
    public string Area => "Sistema";
    public string? Descricao => "Quem alterou o quê e quando (inclusive logins e exportações).";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        // "ids" = registros selecionados na tela: exporta só eles, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros).ToList();
        var filtros = ids.Count > 0 ? new FiltrosDoHistorico() : FiltrosDoHistorico.DosParametros(parametros);
        var consulta = filtros.Aplicar(db.Registros.AsNoTracking(), empresaAtiva.EmpresaIds);
        if (ids.Count > 0) consulta = consulta.Where(r => ids.Contains(r.Id));
        var registros = await consulta.Take(MaximoDeLinhas).ToListAsync(ct);

        var linhas = registros.Select(r => (IReadOnlyList<object?>)
        [
            r.OcorridoEm,
            FiltrosDoHistorico.NomeDoTipo(r.Tipo),
            r.Descricao,
            r.UsuarioNome,
            r.Ip,
            Detalhar(r),
        ]).ToList();

        return new DadosDoRelatorio(
            Titulo,
            [
                new ColunaRelatorio("Data/hora", TipoColuna.DataHora, Largura: 1.1f),
                new ColunaRelatorio("Tipo", Largura: 0.9f),
                new ColunaRelatorio("Descrição", Largura: 2.2f),
                new ColunaRelatorio("Usuário", Largura: 1.3f),
                new ColunaRelatorio("IP", Largura: 1f),
                new ColunaRelatorio("Detalhes", Largura: 4.5f),
            ],
            linhas,
            ids.Count > 0 ? [ParametrosDoRelatorio.Selecionados(registros.Count, "registro", "registros")] : Descrever(filtros));
    }

    /// <summary>Inclusão: "Campo: valor"; exclusão: "Campo: valor anterior"; alteração: "Campo: antes → depois".</summary>
    private static string Detalhar(RegistroAuditoria r) => string.Join("; ", r.Alteracoes
        .Where(a => a.Campo != "Id") // a chave já identifica o registro
        .Select(a => r.Tipo switch
        {
            TipoRegistroAuditoria.Inclusao => $"{a.Campo}: {a.Depois ?? "—"}",
            TipoRegistroAuditoria.Exclusao => $"{a.Campo}: {a.Antes ?? "—"}",
            _ => $"{a.Campo}: {a.Antes ?? "—"} → {a.Depois ?? "—"}",
        }));

    private static List<FiltroAplicado> Descrever(FiltrosDoHistorico f)
    {
        var lista = new List<FiltroAplicado>();
        if (f.Tipo is { } tipo) lista.Add(new("Tipo", FiltrosDoHistorico.NomeDoTipo(tipo)));
        if (f.Entidade is not null) lista.Add(new("Registro", f.Entidade));
        if (f.Evento is not null) lista.Add(new("Evento", f.Evento));
        if (f.De is not null || f.Ate is not null)
            lista.Add(new("Período", $"{f.De?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "início"} a {f.Ate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "hoje"}"));
        return lista;
    }
}
