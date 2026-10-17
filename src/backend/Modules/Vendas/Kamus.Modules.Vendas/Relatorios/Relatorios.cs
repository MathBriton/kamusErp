using System.Globalization;
using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Kamus.SharedKernel.Tempo;

namespace Kamus.Modules.Vendas.Relatorios;

internal static class Parametros
{
    public static DateOnly? Data(string? texto) => DateOnly.TryParse(texto, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static (DateOnly De, DateOnly Ate) Periodo(IReadOnlyDictionary<string, string?> p, TimeProvider relogio)
    {
        var hoje = CalendarioBrasilia.Hoje(relogio);
        return (Data(p.GetValueOrDefault("de")) ?? new DateOnly(hoje.Year, hoje.Month, 1), Data(p.GetValueOrDefault("ate")) ?? hoje);
    }
}

/// <summary>Vendas do período com os mesmos filtros da tela (o Vendedor só vê as suas).</summary>
internal sealed class RelatorioDeVendas(VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "vendas";
    public string Titulo => "Vendas";
    public IReadOnlyCollection<string> Perfis => [.. Permissoes.Registrar, .. Permissoes.Consultar];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Parametros.Periodo(parametros, relogio);
        var status = parametros.GetValueOrDefault("status")?.ToLowerInvariant() is { } s && Consultas.Status.Contains(s) ? s : "todas";
        var vendedorId = Guid.TryParse(parametros.GetValueOrDefault("vendedorId"), out var v) ? v : (Guid?)null;
        var busca = parametros.GetValueOrDefault("busca");
        var r = await Consultas.ListarAsync(db, empresa, usuario.Id, new FiltroDeVendas(de, ate, vendedorId, status, busca), ct);

        var filtros = new List<FiltroAplicado> { new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"), new("Situação", status) };
        if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        filtros.Add(new("Resumo", $"{r.Quantidade} vendas concluídas · {r.Pecas} peças · ticket médio R$ {Dominio.Venda.Moeda(r.TicketMedio)}"));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nº", Largura: 0.5f),
            new ColunaRelatorio("Data", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Cliente", Largura: 1.5f),
            new ColunaRelatorio("Vendedor", Largura: 1.2f),
            new ColunaRelatorio("Peças", TipoColuna.Inteiro, Totalizar: true, Largura: 0.5f),
            new ColunaRelatorio("Pagamento", Largura: 1.6f),
            new ColunaRelatorio("Nota", Largura: 0.8f),
            new ColunaRelatorio("Total", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], r.Itens.Select(l => (IReadOnlyList<object?>)
            [l.Numero, l.Data, l.ClienteNome, l.VendedorNome, l.Pecas, l.Formas, l.NotaExterna, l.Status == "Cancelada" ? 0m : l.Total, l.Status == "Cancelada" ? "Cancelada" : "Concluída"])
            .ToList(), filtros);
    }
}

/// <summary>Comissão por vendedor no período (vendas concluídas).</summary>
internal sealed class RelatorioDeComissoes(VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "comissoes";
    public string Titulo => "Comissões";
    public IReadOnlyCollection<string> Perfis => Permissoes.Consultar;

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Parametros.Periodo(parametros, relogio);
        var comissoes = await Consultas.ComissoesAsync(db, empresa, de, ate, ct);
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Vendedor", Largura: 2f),
            new ColunaRelatorio("Vendas", TipoColuna.Inteiro, Totalizar: true, Largura: 0.6f),
            new ColunaRelatorio("Total vendido", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Comissão", TipoColuna.Moeda, Totalizar: true),
        ], comissoes.Select(c => (IReadOnlyList<object?>)[c.Vendedor, c.Vendas, c.Total, c.Comissao]).ToList(),
        [new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}")]);
    }
}
