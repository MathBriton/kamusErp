using System.Globalization;
using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Kamus.SharedKernel.Tempo;

namespace Kamus.Modules.Vendas.Relatorios;

internal static class Filtros
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
    public string Area => "Vendas";
    public string? Descricao => "Vendas do período com cliente, vendedor, pagamento e total.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("periodo", "Período", TipoDeParametro.Periodo, "mes"), new("status", "Situação", TipoDeParametro.Opcao, "todas", [new("todas", "Todas"), new("concluidas", "Concluídas"), new("canceladas", "Canceladas")])];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, relogio);
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
    public string Area => "Vendas";
    public string? Descricao => "Comissão de cada vendedor no período.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("periodo", "Período", TipoDeParametro.Periodo, "mes")];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, relogio);
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

/// <summary>Vales-troca com saldo e situação (os vencidos com saldo são crédito que o cliente perdeu).</summary>
internal sealed class RelatorioDeVales(VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "vales-troca";
    public string Titulo => "Vales-troca";
    public IReadOnlyCollection<string> Perfis => [.. Permissoes.Registrar, .. Permissoes.Consultar];
    public string Area => "Vendas";
    public string? Descricao => "Vales-troca emitidos com saldo e validade.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("situacao", "Situação", TipoDeParametro.Opcao, "ativos", [new("ativos", "Ativos"), new("vencidos", "Vencidos"), new("usados", "Usados"), new("cancelados", "Cancelados"), new("todos", "Todos")])];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var situacao = parametros.GetValueOrDefault("situacao")?.ToLowerInvariant() is { } s && ValesEndpoints.Situacoes.Contains(s) ? s : "ativos";
        var lista = await ValesEndpoints.ListaAsync(db, empresa, situacao, parametros.GetValueOrDefault("busca"), null, CalendarioBrasilia.Hoje(relogio), ct);
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Código", Largura: 0.8f),
            new ColunaRelatorio("Cliente", Largura: 1.6f),
            new ColunaRelatorio("Emitido em", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Válido até", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Valor", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Saldo", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], lista.Itens.Select(v => (IReadOnlyList<object?>)[v.Codigo, v.ClienteNome, v.EmitidoEm, v.ValidoAte, v.Valor, v.Saldo, v.Situacao]).ToList(),
        [
            new("Situação", situacao),
            new("Saldos", $"ativos R$ {Dominio.Dinheiro.Formatar(lista.SaldoAtivo)} · vencidos R$ {Dominio.Dinheiro.Formatar(lista.SaldoVencido)}"),
        ]);
    }
}

/// <summary>Condicionais com cliente, prazo, peças e valor (as abertas são mercadoria "na rua").</summary>
internal sealed class RelatorioDeCondicionais(VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "condicionais";
    public string Titulo => "Condicionais";
    public IReadOnlyCollection<string> Perfis => [.. Permissoes.Registrar, .. Permissoes.Consultar];
    public string Area => "Vendas";
    public string? Descricao => "Condicionais com cliente, peças, valor e prazo de devolução.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("situacao", "Situação", TipoDeParametro.Opcao, "abertas", [new("abertas", "Abertas"), new("atrasadas", "Atrasadas"), new("fechadas", "Fechadas"), new("todas", "Todas")])];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var situacao = parametros.GetValueOrDefault("situacao")?.ToLowerInvariant() is { } s && CondicionaisEndpoints.Situacoes.Contains(s) ? s : "abertas";
        var lista = await CondicionaisEndpoints.ListaAsync(db, empresa, situacao, null, parametros.GetValueOrDefault("busca"), CalendarioBrasilia.Hoje(relogio), ct);
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nº", Largura: 0.5f),
            new ColunaRelatorio("Cliente", Largura: 1.8f),
            new ColunaRelatorio("Vendedor", Largura: 1.2f),
            new ColunaRelatorio("Saída", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Prazo", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Peças", TipoColuna.Inteiro, Totalizar: true, Largura: 0.5f),
            new ColunaRelatorio("Valor", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], lista.Itens.Select(c => (IReadOnlyList<object?>)[c.Numero, c.ClienteNome, c.VendedorNome, c.Data, c.Prazo, c.Pecas, c.Valor, c.Situacao]).ToList(),
        [
            new("Situação", situacao),
            new("Na rua", $"{lista.PecasNaRua} peças · R$ {Dominio.Dinheiro.Formatar(lista.ValorNaRua)} · {lista.Atrasadas} atrasada(s)"),
        ]);
    }
}
