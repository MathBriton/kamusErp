using Kamus.Modules.Financeiro.Aplicacao;
using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Consultas;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Endpoints;

/// <summary>
/// Crediário próprio: carteira com faixas de atraso, situação e parcelas do cliente, limite, recebimento com multa/juros,
/// carnê e recibo. Vendedor consulta e recebe; limite e regras só Dono/Gerente.
/// </summary>
internal static class CrediarioEndpoints
{
    public static readonly string[] Perfis = ["Dono", "Gerente", "Financeiro", "Vendedor"];
    private static readonly string[] Gestao = ["Dono", "Gerente"];

    public sealed record LimiteRequest(string? ClienteNome, decimal Limite, bool Bloqueado, string? Observacao);
    public sealed record ReceberRequest(DateOnly? Data, Guid ContaId, decimal Valor, decimal? Juros, decimal? Multa, decimal Desconto);
    public sealed record ConfiguracaoDto(decimal MultaPercentual, decimal JurosMensalPercentual, int DiasParaBloquear);

    public sealed record ParcelaDto(Guid ParcelaId, Guid TituloId, string Descricao, int Numero, int Parcelas, DateOnly Vencimento, decimal Valor, decimal Saldo,
        string Situacao, int DiasDeAtraso, decimal Multa, decimal Juros);
    public sealed record ClienteDto(Guid ClienteId, string ClienteNome, decimal Limite, decimal EmAberto, decimal Disponivel, int DiasDeAtraso, bool Bloqueado,
        string? Motivo, bool BloqueadoManualmente, string? Observacao, IReadOnlyList<ParcelaDto> Parcelas);
    public sealed record LinhaDaCarteira(Guid ClienteId, string ClienteNome, decimal Limite, decimal EmAberto, decimal Atrasado, int DiasDeAtraso, decimal Disponivel,
        bool Bloqueado);
    /// <summary>
    /// Faixas e <c>Total</c> (saldo em aberto) sempre sobre toda a carteira. <c>Clientes</c> é a página pedida (ou todos, sem <c>pagina</c>);
    /// <c>Registros</c> é quantos clientes a busca traz.
    /// </summary>
    public sealed record CarteiraDto(decimal AVencer, decimal Ate30, decimal Ate60, decimal Ate90, decimal Acima90, decimal Total, IReadOnlyList<LinhaDaCarteira> Clientes,
        int Registros, int Pagina, int Tamanho);
    public sealed record RecebimentoDto(Guid BaixaId, decimal Valor, decimal Multa, decimal Juros, decimal Desconto, decimal Total);
    public sealed record ContaResumo(Guid Id, string Nome);

    public static IEndpointRouteBuilder MapCrediario(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/financeiro/crediario").RequireAuthorization().WithTags("Crediário");
        g.MapGet("/carteira", Carteira).WithSummary("Carteira do crediário: saldo por faixa de atraso e por cliente (busca opcional; pagina e tamanho)");
        g.MapGet("/clientes/{id:guid}", Cliente).WithSummary("Situação do cliente (limite, em aberto, disponível, bloqueio) e parcelas com encargos de hoje");
        g.MapPut("/clientes/{id:guid}/limite", DefinirLimite).WithSummary("Define o limite e o bloqueio manual do cliente (Dono/Gerente)");
        g.MapGet("/clientes/{id:guid}/carne.pdf", Carne).WithSummary("Carnê em PDF com as parcelas em aberto do cliente");
        g.MapPost("/parcelas/{id:guid}/receber", Receber).WithSummary("Recebe a parcela (total ou parcial); sem multa/juros informados, usa os calculados");
        g.MapGet("/baixas/{id:guid}/recibo.pdf", Recibo).WithSummary("Recibo em PDF de um recebimento do crediário");
        g.MapGet("/contas", Contas).WithSummary("Contas financeiras ativas da empresa (para escolher onde o dinheiro entra)");
        g.MapGet("/configuracao", ObterConfiguracao).WithSummary("Multa, juros e dias de atraso que bloqueiam");
        g.MapPut("/configuracao", DefinirConfiguracao).WithSummary("Altera as regras do crediário (Dono/Gerente)");
        return app;
    }

    private static IResult? Barrar(IEmpresaAtiva empresa, string[] perfis, string detalhe) =>
        perfis.Contains(empresa.Perfil) ? null : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: detalhe);

    private static IResult? BarrarConsulta(IEmpresaAtiva empresa) => Barrar(empresa, Perfis, "Seu perfil não acessa o crediário.");

    private static IEnumerable<ParcelaDto> Parcelas(Titulo t, DateOnly hoje, ConfiguracaoDoCrediario config) => t.Parcelas.OrderBy(p => p.Numero).Select(p =>
    {
        var encargos = p.Saldo > 0 ? Encargos.Calcular(p.Saldo, p.Vencimento, hoje, config) : new Encargos(0, 0, 0);
        return new ParcelaDto(p.Id, t.Id, t.Descricao, p.Numero, t.Parcelas.Count, p.Vencimento, p.Valor, p.Saldo, p.Situacao(hoje).ToString(), encargos.DiasDeAtraso,
            encargos.Multa, encargos.Juros);
    });

    private static async Task<IResult> Carteira(string? busca, int? pagina, int? tamanho, FinanceiroDbContext db, Crediario crediario, IEmpresaAtiva empresa,
        CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var hoje = crediario.Hoje;
        var config = await crediario.ConfiguracaoAsync(ct);
        var titulos = await crediario.TitulosAsync(null, empresa.EmpresaIds, ct);
        var limites = await db.LimitesDeCredito.AsNoTracking().ToDictionaryAsync(l => l.ClienteId, ct);
        var abertas = titulos.SelectMany(t => t.Parcelas.Where(p => p.Saldo > 0).Select(p => (Titulo: t, Parcela: p))).ToList();
        decimal Faixa(FaixaDeAtraso f) => abertas.Where(x => Credito.Faixa(hoje.DayNumber - x.Parcela.Vencimento.DayNumber) == f).Sum(x => x.Parcela.Saldo);

        var termo = TextoParaBusca.Normalizar(busca);
        var clientes = titulos.Where(t => t.PessoaId is not null).Select(t => t.PessoaId!.Value).Concat(limites.Keys).Distinct()
            .Select(id =>
            {
                var dele = titulos.Where(t => t.PessoaId == id).ToList();
                var limite = limites.GetValueOrDefault(id);
                var situacao = Credito.Avaliar(limite, dele.SelectMany(t => t.Parcelas).Sum(p => p.Saldo), crediario.DiasDeAtraso(dele), config);
                var nome = limite?.ClienteNome ?? dele.Select(t => t.PessoaNome).FirstOrDefault(n => n is not null) ?? "?";
                var atrasado = dele.SelectMany(t => t.Parcelas).Where(p => p.Saldo > 0 && p.Vencimento < hoje).Sum(p => p.Saldo);
                return new LinhaDaCarteira(id, nome, situacao.Limite, situacao.EmAberto, atrasado, situacao.DiasDeAtraso, situacao.Disponivel, situacao.Bloqueado);
            })
            .Where(l => string.IsNullOrEmpty(termo) || TextoParaBusca.Normalizar(l.ClienteNome).Contains(termo, StringComparison.Ordinal))
            .OrderByDescending(l => l.DiasDeAtraso).ThenByDescending(l => l.EmAberto).ThenBy(l => l.ClienteNome, StringComparer.CurrentCulture).ToList();
        // Sem página, todos os clientes (outros chamadores dependem da lista completa).
        var pag = pagina is null ? new PaginaDeRegistros<LinhaDaCarteira>(clientes, clientes.Count, 1, clientes.Count) : clientes.Paginar(pagina.Value, tamanho ?? Paginacao.TamanhoPadrao);
        return Results.Ok(new CarteiraDto(Faixa(FaixaDeAtraso.AVencer), Faixa(FaixaDeAtraso.Ate30), Faixa(FaixaDeAtraso.Ate60), Faixa(FaixaDeAtraso.Ate90),
            Faixa(FaixaDeAtraso.Acima90), abertas.Sum(x => x.Parcela.Saldo), pag.Itens, pag.Total, pag.Pagina, pag.Tamanho));
    }

    private static async Task<IResult> Cliente(Guid id, FinanceiroDbContext db, Crediario crediario, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var titulos = await crediario.TitulosAsync(id, null, ct);
        var limite = await db.LimitesDeCredito.AsNoTracking().SingleOrDefaultAsync(l => l.ClienteId == id, ct);
        var config = await crediario.ConfiguracaoAsync(ct);
        var situacao = Credito.Avaliar(limite, titulos.SelectMany(t => t.Parcelas).Sum(p => p.Saldo), crediario.DiasDeAtraso(titulos), config);
        var nome = limite?.ClienteNome ?? titulos.Select(t => t.PessoaNome).FirstOrDefault(n => n is not null) ?? "";
        var visiveis = empresa.EmpresaIds;
        var parcelas = titulos.Where(t => visiveis.Contains(t.EmpresaId)).SelectMany(t => Parcelas(t, crediario.Hoje, config))
            .OrderBy(p => p.Vencimento).ThenBy(p => p.Numero).ToList();
        return Results.Ok(new ClienteDto(id, nome, situacao.Limite, situacao.EmAberto, situacao.Disponivel, situacao.DiasDeAtraso, situacao.Bloqueado, situacao.Motivo,
            limite?.Bloqueado ?? false, limite?.Observacao, parcelas));
    }

    private static async Task<IResult> DefinirLimite(Guid id, LimiteRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        IRegistradorDeEventos eventos, CancellationToken ct)
    {
        if (Barrar(empresa, Gestao, "Só Dono e Gerente definem o limite do crediário.") is { } negado) return negado;
        if (string.IsNullOrWhiteSpace(r.ClienteNome)) return Permissoes.Erro("Informe o nome do cliente.");
        var limite = await db.LimitesDeCredito.SingleOrDefaultAsync(l => l.ClienteId == id, ct);
        try
        {
            if (limite is null)
            {
                limite = LimiteDeCredito.Definir(id, r.ClienteNome, r.Limite, r.Bloqueado, r.Observacao, usuario.Nome);
                db.LimitesDeCredito.Add(limite);
            }
            else limite.Atualizar(r.ClienteNome, r.Limite, r.Bloqueado, r.Observacao, usuario.Nome);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("limite_de_credito", $"Limite do crediário de {limite.ClienteNome}: R$ {Titulo.Moeda(limite.Limite)}{(limite.Bloqueado ? " (bloqueado)" : "")}", ct: ct);
        return Results.Ok();
    }

    private static async Task<(Titulo Titulo, Parcela Parcela)?> ParcelaAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, Guid parcelaId, CancellationToken ct)
    {
        var tituloId = await db.Parcelas.Where(p => p.Id == parcelaId).Select(p => (Guid?)p.TituloId).SingleOrDefaultAsync(ct);
        if (tituloId is null) return null;
        var ids = empresa.EmpresaIds;
        var titulo = await db.Titulos.Include(t => t.Parcelas).ThenInclude(p => p.Baixas)
            .SingleOrDefaultAsync(t => t.Id == tituloId && t.Crediario && ids.Contains(t.EmpresaId), ct);
        if (titulo is null) return null;
        titulo.Reconectar();
        return (titulo, titulo.Parcelas.Single(p => p.Id == parcelaId));
    }

    private static async Task<IResult> Receber(Guid id, ReceberRequest r, FinanceiroDbContext db, Crediario crediario, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        TimeProvider relogio, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        if (await ParcelaAsync(db, empresa, id, ct) is not { } achada) return Results.NotFound();
        var (titulo, parcela) = achada;
        var data = r.Data ?? crediario.Hoje;
        if (data > crediario.Hoje) return Permissoes.Erro("A data do recebimento não pode ser futura.");
        var conta = await db.Contas.SingleOrDefaultAsync(c => c.Id == r.ContaId, ct);
        if (conta is null || conta.EmpresaId != titulo.EmpresaId || !conta.Ativa) return Permissoes.Erro("Escolha uma conta financeira ativa da mesma empresa da venda.");

        // Encargos proporcionais ao valor pago, calculados sobre o saldo em atraso.
        var sugeridos = Encargos.Calcular(Math.Min(r.Valor, parcela.Saldo), parcela.Vencimento, data, await crediario.ConfiguracaoAsync(ct));
        try
        {
            var baixa = parcela.Baixar(data, conta.Id, r.Valor, r.Juros ?? sugeridos.Juros, r.Multa ?? sugeridos.Multa, r.Desconto, usuario.Nome, relogio.GetUtcNow());
            db.Baixas.Add(baixa);
            db.Movimentos.Add(MovimentoFinanceiro.DaBaixa(titulo, parcela, baixa, relogio.GetUtcNow()));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new RecebimentoDto(baixa.Id, baixa.Valor, baixa.Multa, baixa.Juros, baixa.Desconto, baixa.Total));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }
    }

    private static async Task<IResult> Recibo(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var parcelaId = await db.Baixas.Where(b => b.Id == id).Select(b => (Guid?)b.ParcelaId).SingleOrDefaultAsync(ct);
        if (parcelaId is null || await ParcelaAsync(db, empresa, parcelaId.Value, ct) is not { } achada) return Results.NotFound();
        var (titulo, parcela) = achada;
        var baixa = parcela.Baixas.Single(b => b.Id == id);
        var pdf = DocumentosDoCrediario.Recibo(new DadosDoRecibo(empresa.Nome ?? "", empresa.Cnpj, titulo.PessoaNome ?? "", titulo.Descricao, parcela.Numero,
            titulo.Parcelas.Count, parcela.Vencimento, baixa.Data, baixa.Valor, baixa.Multa, baixa.Juros, baixa.Desconto, parcela.Saldo, baixa.Usuario));
        return Results.File(pdf, "application/pdf", $"recibo-{parcela.Numero}-{baixa.Data:yyyyMMdd}.pdf");
    }

    private static async Task<IResult> Carne(Guid id, Crediario crediario, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var titulos = await crediario.TitulosAsync(id, empresa.EmpresaIds, ct);
        var parcelas = titulos.SelectMany(t => t.Parcelas.Where(p => p.Saldo > 0).Select(p => new ParcelaDoCarne(t.Descricao, p.Numero, t.Parcelas.Count, p.Vencimento, p.Saldo)))
            .OrderBy(p => p.Vencimento).ToList();
        var cliente = titulos.Select(t => t.PessoaNome).FirstOrDefault(n => n is not null) ?? "Cliente";
        return Results.File(DocumentosDoCrediario.Carne(empresa.Nome ?? "", empresa.Cnpj, cliente, parcelas), "application/pdf", "carne.pdf");
    }

    private static async Task<IResult> Contas(FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var empresaId = empresa.EmpresaId;
        var contas = await db.Contas.AsNoTracking().Where(c => c.EmpresaId == empresaId && c.Ativa).ToListAsync(ct);
        return Results.Ok(contas.OrderBy(c => c.Nome, StringComparer.CurrentCulture).Select(c => new ContaResumo(c.Id, c.Nome)).ToList());
    }

    private static async Task<IResult> ObterConfiguracao(Crediario crediario, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (BarrarConsulta(empresa) is { } negado) return negado;
        var c = await crediario.ConfiguracaoAsync(ct);
        return Results.Ok(new ConfiguracaoDto(c.MultaPercentual, c.JurosMensalPercentual, c.DiasParaBloquear));
    }

    private static async Task<IResult> DefinirConfiguracao(ConfiguracaoDto r, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa, Gestao, "Só Dono e Gerente alteram as regras do crediário.") is { } negado) return negado;
        var config = await db.ConfiguracoesDoCrediario.SingleOrDefaultAsync(ct);
        if (config is null)
        {
            config = ConfiguracaoDoCrediario.Padrao();
            db.ConfiguracoesDoCrediario.Add(config);
        }
        try
        {
            config.Definir(r.MultaPercentual, r.JurosMensalPercentual, r.DiasParaBloquear);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new ConfiguracaoDto(config.MultaPercentual, config.JurosMensalPercentual, config.DiasParaBloquear));
    }
}
