using Kamus.Modules.Financeiro.Aplicacao;
using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Endpoints;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;
using Kamus.SharedKernel.Tempo;

namespace Kamus.Modules.Financeiro.Relatorios;

/// <summary>Contas a pagar ou a receber (uma linha por parcela) com os mesmos filtros da tela.</summary>
internal sealed class RelatorioDeTitulos(FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "titulos";
    public string Titulo => "Contas a pagar e a receber";
    public IReadOnlyCollection<string> Perfis => Permissoes.Financeiro;

    private static DateOnly? Data(string? texto) => DateOnly.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var tipo = TitulosEndpoints.Tipo(parametros.GetValueOrDefault("tipo")) ?? TipoDeTitulo.Pagar;
        var situacao = parametros.GetValueOrDefault("situacao")?.ToLowerInvariant() is { } s && Consultas.Situacoes.Contains(s) ? s : "abertas";
        var (de, ate, busca) = (Data(parametros.GetValueOrDefault("de")), Data(parametros.GetValueOrDefault("ate")), parametros.GetValueOrDefault("busca"));
        var r = await Consultas.ParcelasAsync(db, empresa, new FiltroDeTitulos(tipo, situacao, de, ate, busca), CalendarioBrasilia.Hoje(relogio), ct);

        var filtros = new List<FiltroAplicado> { new("Situação", situacao) };
        if (de is not null || ate is not null) filtros.Add(new("Vencimento", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"));
        if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new("Busca", busca));
        return new DadosDoRelatorio(tipo == TipoDeTitulo.Pagar ? "Contas a pagar" : "Contas a receber",
        [
            new ColunaRelatorio("Vencimento", TipoColuna.Data, Largura: 0.8f),
            new ColunaRelatorio("Descrição", Largura: 2f),
            new ColunaRelatorio("Parcela", Largura: 0.5f),
            new ColunaRelatorio(tipo == TipoDeTitulo.Pagar ? "Fornecedor" : "Cliente", Largura: 1.4f),
            new ColunaRelatorio("Categoria", Largura: 1.1f),
            new ColunaRelatorio("Documento", Largura: 0.8f),
            new ColunaRelatorio("Valor", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Pago", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Saldo", TipoColuna.Moeda, Totalizar: true, Largura: 0.8f),
            new ColunaRelatorio("Situação", Largura: 0.7f),
        ], r.Itens.Select(l => (IReadOnlyList<object?>)
            [l.Vencimento, l.Descricao, $"{l.Numero}/{l.Parcelas}", l.PessoaNome, l.Categoria, l.Documento, l.Valor, l.ValorPago, l.Saldo, l.Situacao]).ToList(), filtros);
    }
}

/// <summary>Fluxo de caixa por dia (ou mês): realizado, previsto e saldo acumulado.</summary>
internal sealed class RelatorioDeFluxo(FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio) : IRelatorio
{
    public string Codigo => "fluxo-de-caixa";
    public string Titulo => "Fluxo de caixa";
    public IReadOnlyCollection<string> Perfis => Permissoes.Financeiro;

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var hoje = CalendarioBrasilia.Hoje(relogio);
        static DateOnly? Data(string? t) => DateOnly.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        var de = Data(parametros.GetValueOrDefault("de")) ?? hoje;
        var ate = Data(parametros.GetValueOrDefault("ate")) ?? de.AddDays(30);
        if (ate < de || ate.DayNumber - de.DayNumber > 731) ate = de.AddDays(30);
        var contaId = Guid.TryParse(parametros.GetValueOrDefault("contaId"), out var c) ? c : (Guid?)null;
        var f = await Consultas.FluxoAsync(db, empresa, de, ate, contaId, hoje, ct);

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio(f.Agrupamento == "mes" ? "Mês" : "Data", TipoColuna.Data, Largura: 0.9f),
            new ColunaRelatorio("Entradas realizadas", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Saídas realizadas", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Entradas previstas", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Saídas previstas", TipoColuna.Moeda, Totalizar: true),
            new ColunaRelatorio("Saldo", TipoColuna.Moeda),
        ], f.Periodos.Select(p => (IReadOnlyList<object?>)[p.Data, p.EntradasRealizadas, p.SaidasRealizadas, p.EntradasPrevistas, p.SaidasPrevistas, p.Saldo]).ToList(),
        [
            new("Período", $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"),
            new("Saldo inicial", $"R$ {Dominio.Titulo.Moeda(f.SaldoInicial)}"),
            new("Em atraso", $"a receber R$ {Dominio.Titulo.Moeda(f.AReceberEmAtraso)} · a pagar R$ {Dominio.Titulo.Moeda(f.APagarEmAtraso)}"),
        ]);
    }
}
