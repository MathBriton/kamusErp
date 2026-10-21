using System.Globalization;
using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Indicadores;
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
        // "ids" = linhas selecionadas na grade: exporta só elas, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var filtro = ids.Count > 0 ? new FiltroDeVendas(null, null, null, "todas", null, Ids: ids) : new FiltroDeVendas(de, ate, vendedorId, status, busca);
        var r = await Consultas.ListarAsync(db, empresa, usuario.Id, filtro, ct);

        var filtros = ids.Count > 0
            ? [ParametrosDoRelatorio.Selecionados(r.Registros, "venda", "vendas")]
            : new List<FiltroAplicado> { new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"), new("Situação", status) };
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
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
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var lista = await ValesEndpoints.ListaAsync(db, empresa, situacao, parametros.GetValueOrDefault("busca"), null, CalendarioBrasilia.Hoje(relogio), ct, selecionados: ids);
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
            ids.Count > 0 ? ParametrosDoRelatorio.Selecionados(lista.Total, "vale", "vales") : new("Situação", situacao),
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
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var busca = parametros.GetValueOrDefault("busca");
        var lista = await CondicionaisEndpoints.ListaAsync(db, empresa, situacao, null, busca, CalendarioBrasilia.Hoje(relogio), ct, selecionadas: ids);
        var filtros = new List<FiltroAplicado> { ids.Count > 0 ? ParametrosDoRelatorio.Selecionados(lista.Total, "condicional", "condicionais") : new("Situação", situacao) };
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        filtros.Add(new("Na rua", $"{lista.PecasNaRua} peças · R$ {Dominio.Dinheiro.Formatar(lista.ValorNaRua)} · {lista.Atrasadas} atrasada(s)"));
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
        ], lista.Itens.Select(c => (IReadOnlyList<object?>)[c.Numero, c.ClienteNome, c.VendedorNome, c.Data, c.Prazo, c.Pecas, c.Valor, c.Situacao]).ToList(), filtros);
    }
}

/// <summary>Trocas do período com venda de origem, motivo, peças devolvidas, diferença e vale emitido.</summary>
internal sealed class RelatorioDeTrocas(VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "trocas";
    public string Titulo => "Trocas";
    public IReadOnlyCollection<string> Perfis => [.. Permissoes.Registrar, .. Permissoes.Consultar];
    public string Area => "Vendas";
    public string? Descricao => "Trocas do período com a venda de origem, motivo, diferença e vale-troca emitido.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros => [new("periodo", "Período", TipoDeParametro.Periodo, "mes")];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var (de, ate) = Filtros.Periodo(parametros, relogio);
        var busca = parametros.GetValueOrDefault("busca");
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var trocas = await TrocasEndpoints.ListaAsync(db, empresa, usuario.Id, de, ate, busca, null, ct, ids);
        var filtros = new List<FiltroAplicado>
        {
            ids.Count > 0 ? ParametrosDoRelatorio.Selecionados(trocas.Count, "troca", "trocas") : new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"),
        };
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nº", Largura: 0.5f),
            new ColunaRelatorio("Data", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Venda", Largura: 0.6f),
            new ColunaRelatorio("Cliente", Largura: 1.6f),
            new ColunaRelatorio("Motivo", Largura: 0.9f),
            new ColunaRelatorio("Peças devolvidas", TipoColuna.Inteiro, Totalizar: true, Largura: 0.7f),
            new ColunaRelatorio("Devolvido", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Levado", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Diferença", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Vale-troca", Largura: 0.8f),
        ], trocas.Select(t => (IReadOnlyList<object?>)
            [t.Numero, t.Data, t.VendaNumero, t.ClienteNome, t.Motivo, t.PecasDevolvidas, t.ValorDevolvido, t.ValorLevado, t.Diferenca, t.ValeCodigo]).ToList(), filtros);
    }
}

/// <summary>Formas de pagamento com tipo, conta de entrada, taxas, parcelas e prazo de recebimento.</summary>
internal sealed class RelatorioDeFormas(VendasDbContext db, IEmpresaAtiva empresa, IIndicadoresFinanceiros financeiro, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "formas-de-pagamento";
    public string Titulo => "Formas de pagamento";
    public IReadOnlyCollection<string> Perfis => [.. Permissoes.Registrar, .. Permissoes.Consultar];
    public string Area => "Vendas";
    public string? Descricao => "Formas de pagamento com taxas, parcelas, prazo e a conta onde o dinheiro entra.";
    public IReadOnlyList<ParametroDoRelatorio> Parametros =>
        [new("situacao", "Situação", TipoDeParametro.Opcao, "todas", [new("ativas", "Ativas"), new("inativas", "Inativas"), new("todas", "Todas")])];

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var situacao = parametros.GetValueOrDefault("situacao")?.ToLowerInvariant() is { } s && FormasEndpoints.Situacoes.Contains(s) ? s : "todas";
        var busca = parametros.GetValueOrDefault("busca");
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var formas = await FormasEndpoints.ListaAsync(db, empresa, situacao, busca, ct, ids);
        var contas = (await financeiro.ResumoAsync(empresa.EmpresaIds, CalendarioBrasilia.Hoje(relogio), ct)).Contas.ToDictionary(c => c.Id, c => c.Nome);
        var filtros = new List<FiltroAplicado> { ids.Count > 0 ? ParametrosDoRelatorio.Selecionados(formas.Count, "forma", "formas") : new("Situação", situacao) };
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Forma", Largura: 1.6f),
            new ColunaRelatorio("Tipo", Largura: 0.8f),
            new ColunaRelatorio("Conta de entrada", Largura: 1.4f),
            new ColunaRelatorio("Taxa à vista (%)", TipoColuna.Numero, Largura: 0.7f),
            new ColunaRelatorio("Taxa parcelado (%)", TipoColuna.Numero, Largura: 0.7f),
            new ColunaRelatorio("Máx. parcelas", TipoColuna.Inteiro, Largura: 0.6f),
            new ColunaRelatorio("Prazo (dias)", TipoColuna.Inteiro, Largura: 0.6f),
            new ColunaRelatorio("Situação", Largura: 0.6f),
        ], formas.Select(f => (IReadOnlyList<object?>)
            [f.Nome, f.Tipo, f.ContaId is { } c ? contas.GetValueOrDefault(c) : null, f.TaxaAVista, f.TaxaParcelado, f.MaxParcelas, f.PrazoDias, f.Ativa ? "Ativa" : "Inativa"])
            .ToList(), filtros);
    }
}

/// <summary>Fechamentos de comissão: vendedor, período, vendas e trocas incluídas, comissão e se gerou conta a pagar.</summary>
internal sealed class RelatorioDeFechamentos(VendasDbContext db, IEmpresaAtiva empresa) : IRelatorio
{
    public string Codigo => "fechamentos-de-comissao";
    public string Titulo => "Fechamentos de comissão";
    public IReadOnlyCollection<string> Perfis => Permissoes.Consultar;
    public string Area => "Vendas";
    public string? Descricao => "Comissões já fechadas por vendedor e período, com a conta a pagar gerada.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var vendedorId = ids.Count == 0 && Guid.TryParse(parametros.GetValueOrDefault("vendedorId"), out var v) ? v : (Guid?)null;
        var fechamentos = await FechamentosEndpoints.ListaAsync(db, empresa, vendedorId, ct, ids);
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Vendedor", Largura: 1.6f),
            new ColunaRelatorio("De", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Até", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Vendas", TipoColuna.Inteiro, Totalizar: true, Largura: 0.5f),
            new ColunaRelatorio("Trocas", TipoColuna.Inteiro, Totalizar: true, Largura: 0.5f),
            new ColunaRelatorio("Total vendido", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
            new ColunaRelatorio("Comissão", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Conta a pagar", Largura: 0.7f),
            new ColunaRelatorio("Fechado por", Largura: 1f),
        ], fechamentos.Select(f => (IReadOnlyList<object?>)
            [f.VendedorNome, f.De, f.Ate, f.Vendas, f.Trocas, f.TotalVendido, f.Comissao, f.ContaAPagarGerada ? "Gerada" : "Não", f.FechadoPor]).ToList(),
        ids.Count > 0 ? [ParametrosDoRelatorio.Selecionados(fechamentos.Count, "fechamento", "fechamentos")] : []);
    }
}
