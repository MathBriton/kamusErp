using Kamus.Modules.Financeiro.Aplicacao;
using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Endpoints;

/// <summary>Contas a pagar e a receber: títulos com parcelas, baixas (parciais, com juros/multa/desconto), estorno e cancelamento.</summary>
internal static class TitulosEndpoints
{
    public sealed record ParcelaRequest(DateOnly Vencimento, decimal Valor);

    /// <param name="RepetirMeses">Despesa/receita fixa: cria um título por mês (2 a 24), com as parcelas deslocadas mês a mês.</param>
    public sealed record TituloRequest(string? Tipo, string? Descricao, Guid? PessoaId, string? PessoaNome, Guid? CategoriaId, string? Documento,
        DateOnly? Competencia, string? Observacao, IReadOnlyList<ParcelaRequest>? Parcelas, int? RepetirMeses);
    public sealed record BaixaRequest(DateOnly? Data, Guid ContaId, decimal Valor, decimal Juros, decimal Multa, decimal Desconto);

    public sealed record BaixaDto(Guid Id, DateOnly Data, Guid ContaId, string Conta, decimal Valor, decimal Juros, decimal Multa, decimal Desconto, decimal Total,
        string? Usuario, bool Estornada, string? EstornadaPor);
    public sealed record ParcelaDto(Guid Id, int Numero, DateOnly Vencimento, decimal Valor, decimal ValorPago, decimal Saldo, string Situacao, IReadOnlyList<BaixaDto> Baixas);
    public sealed record TituloDto(Guid Id, Guid EmpresaId, string Tipo, string Descricao, Guid? PessoaId, string? PessoaNome, Guid? CategoriaId, string? Categoria,
        string? Documento, DateOnly Competencia, string? Observacao, decimal ValorTotal, bool Cancelado, string Origem, IReadOnlyList<ParcelaDto> Parcelas);
    public sealed record ListaDto(IReadOnlyList<LinhaDeParcela> Itens, decimal Valor, decimal Pago, decimal Saldo, decimal Vencido);

    public static IEndpointRouteBuilder MapTitulos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/financeiro").RequireAuthorization().WithTags("Financeiro");
        g.MapGet("/titulos", Listar).WithSummary("Parcelas de contas a pagar ou a receber. Filtros: tipo, situacao (abertas, vencidas, pagas, canceladas, todas), de, ate, busca");
        g.MapGet("/titulos/{id:guid}", Obter).WithSummary("Título com parcelas e baixas");
        g.MapPost("/titulos", Criar).WithSummary("Novo título com parcelas (repetirMeses para despesas fixas)").Produces<TituloDto>(StatusCodes.Status201Created);
        g.MapPut("/titulos/{id:guid}", Atualizar).WithSummary("Altera descrição, pessoa, categoria, documento, competência e observação");
        g.MapPost("/titulos/{id:guid}/cancelar", Cancelar).WithSummary("Cancela o título (só sem baixas ativas)");
        g.MapPost("/parcelas/{id:guid}/baixas", Baixar).WithSummary("Paga/recebe a parcela (total ou parcial) numa conta financeira").Produces<TituloDto>();
        g.MapPost("/baixas/{id:guid}/estornar", Estornar).WithSummary("Estorna a baixa: devolve o saldo da parcela e lança o movimento contrário na conta");
        return app;
    }

    internal static TipoDeTitulo? Tipo(string? texto) => texto?.ToLowerInvariant() switch
    {
        "pagar" => TipoDeTitulo.Pagar,
        "receber" => TipoDeTitulo.Receber,
        _ => null,
    };

    private static async Task<TituloDto> ParaDtoAsync(FinanceiroDbContext db, Titulo t, DateOnly hoje, CancellationToken ct)
    {
        var contaIds = t.Parcelas.SelectMany(p => p.Baixas.Select(b => b.ContaId)).Distinct().ToList();
        var contas = await db.Contas.AsNoTracking().Where(c => contaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var categoria = t.CategoriaId is { } c ? await db.Categorias.AsNoTracking().Where(x => x.Id == c).Select(x => x.Nome).SingleOrDefaultAsync(ct) : null;
        return new TituloDto(t.Id, t.EmpresaId, t.Tipo.ToString(), t.Descricao, t.PessoaId, t.PessoaNome, t.CategoriaId, categoria, t.Documento, t.Competencia,
            t.Observacao, t.ValorTotal, t.Cancelado, t.Origem,
            t.Parcelas.OrderBy(p => p.Numero).Select(p => new ParcelaDto(p.Id, p.Numero, p.Vencimento, p.Valor, p.ValorPago, t.Cancelado ? 0 : p.Saldo,
                p.Situacao(hoje).ToString(), p.Baixas.OrderBy(b => b.RegistradaEm).Select(b => new BaixaDto(b.Id, b.Data, b.ContaId, contas.GetValueOrDefault(b.ContaId, "?"),
                    b.Valor, b.Juros, b.Multa, b.Desconto, b.Total, b.Usuario, b.Estornada, b.EstornadaPor)).ToList())).ToList());
    }

    private static async Task<Titulo?> CarregarAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, Guid id, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var titulo = await db.Titulos.Include(t => t.Parcelas).ThenInclude(p => p.Baixas).SingleOrDefaultAsync(t => t.Id == id && ids.Contains(t.EmpresaId), ct);
        titulo?.Reconectar();
        return titulo;
    }

    private static async Task<IResult> Listar(string? tipo, string? situacao, DateOnly? de, DateOnly? ate, string? busca, FinanceiroDbContext db,
        IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        if (Tipo(tipo) is not { } t) return Permissoes.Erro("Informe o tipo: pagar ou receber.");
        var s = situacao?.ToLowerInvariant() ?? "abertas";
        if (!Consultas.Situacoes.Contains(s)) return Permissoes.Erro("Situação inválida: abertas, vencidas, pagas, canceladas ou todas.");
        var r = await Consultas.ParcelasAsync(db, empresa, new FiltroDeTitulos(t, s, de, ate, busca), CalendarioBrasilia.Hoje(relogio), ct);
        return Results.Ok(new ListaDto(r.Itens, r.Valor, r.Pago, r.Saldo, r.Vencido));
    }

    private static async Task<IResult> Obter(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var titulo = await CarregarAsync(db, empresa, id, ct);
        return titulo is null ? Results.NotFound() : Results.Ok(await ParaDtoAsync(db, titulo, CalendarioBrasilia.Hoje(relogio), ct));
    }

    private static async Task<IResult?> ValidarCategoriaAsync(FinanceiroDbContext db, TipoDeTitulo tipo, Guid? categoriaId, CancellationToken ct)
    {
        if (categoriaId is null) return null;
        var categoria = await db.Categorias.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoriaId, ct);
        if (categoria is null) return Permissoes.Erro("Categoria não encontrada.");
        var esperado = tipo == TipoDeTitulo.Pagar ? TipoDeCategoria.Despesa : TipoDeCategoria.Receita;
        return categoria.Tipo == esperado ? null
            : Permissoes.Erro($"A categoria {categoria.Nome} é de {categoria.Tipo.ToString().ToLowerInvariant()}; escolha uma de {esperado.ToString().ToLowerInvariant()}.");
    }

    private static DadosDoTitulo Dados(TipoDeTitulo tipo, TituloRequest r, DateOnly hoje) =>
        new(tipo, r.Descricao ?? "", r.PessoaId, r.PessoaNome, r.CategoriaId, r.Documento, r.Competencia ?? hoje, r.Observacao);

    private static async Task<IResult> Criar(TituloRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        if (Tipo(r.Tipo) is not { } tipo) return Permissoes.Erro("Informe o tipo: pagar ou receber.");
        if (await ValidarCategoriaAsync(db, tipo, r.CategoriaId, ct) is { } invalida) return invalida;
        var repetir = r.RepetirMeses ?? 1;
        if (repetir is < 1 or > 24) return Permissoes.Erro("Repetir: de 1 a 24 meses.");
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var parcelas = (r.Parcelas ?? []).Select(p => new NovaParcela(p.Vencimento, p.Valor)).ToList();
        var titulos = new List<Titulo>();
        try
        {
            for (var mes = 0; mes < repetir; mes++)
            {
                var dados = Dados(tipo, r, hoje);
                titulos.Add(Titulo.Criar(empresa.EmpresaId!.Value, dados with { Competencia = dados.Competencia.AddMonths(mes) },
                    parcelas.Select(p => p with { Vencimento = p.Vencimento.AddMonths(mes) }).ToList(), relogio.GetUtcNow()));
            }
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        db.Titulos.AddRange(titulos);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/financeiro/titulos/{titulos[0].Id}", await ParaDtoAsync(db, titulos[0], hoje, ct));
    }

    private static async Task<IResult> Atualizar(Guid id, TituloRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var titulo = await CarregarAsync(db, empresa, id, ct);
        if (titulo is null) return Results.NotFound();
        if (await ValidarCategoriaAsync(db, titulo.Tipo, r.CategoriaId, ct) is { } invalida) return invalida;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        try
        {
            titulo.Atualizar(Dados(titulo.Tipo, r, hoje));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(db, titulo, hoje, ct));
    }

    private static async Task<IResult> Cancelar(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var titulo = await CarregarAsync(db, empresa, id, ct);
        if (titulo is null) return Results.NotFound();
        try
        {
            titulo.Cancelar(usuario.Nome, relogio.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(db, titulo, CalendarioBrasilia.Hoje(relogio), ct));
    }

    private static async Task<IResult> Baixar(Guid id, BaixaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var tituloId = await db.Parcelas.Where(p => p.Id == id).Select(p => (Guid?)p.TituloId).SingleOrDefaultAsync(ct);
        var titulo = tituloId is null ? null : await CarregarAsync(db, empresa, tituloId.Value, ct);
        if (titulo is null) return Results.NotFound();
        var conta = await db.Contas.SingleOrDefaultAsync(c => c.Id == r.ContaId, ct);
        if (conta is null || conta.EmpresaId != titulo.EmpresaId) return Permissoes.Erro("Escolha uma conta financeira da mesma empresa do título.");
        if (!conta.Ativa) return Permissoes.Erro($"{conta.Nome} está desativada.");

        var parcela = titulo.Parcelas.Single(p => p.Id == id);
        var hoje = CalendarioBrasilia.Hoje(relogio);
        try
        {
            var baixa = parcela.Baixar(r.Data ?? hoje, conta.Id, r.Valor, r.Juros, r.Multa, r.Desconto, usuario.Nome, relogio.GetUtcNow());
            db.Baixas.Add(baixa);
            db.Movimentos.Add(MovimentoFinanceiro.DaBaixa(titulo, parcela, baixa, relogio.GetUtcNow()));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(db, titulo, hoje, ct));
    }

    private static async Task<IResult> Estornar(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, IRegistradorDeEventos eventos,
        TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var parcelaId = await db.Baixas.Where(b => b.Id == id).Select(b => (Guid?)b.ParcelaId).SingleOrDefaultAsync(ct);
        var tituloId = parcelaId is null ? null : await db.Parcelas.Where(p => p.Id == parcelaId).Select(p => (Guid?)p.TituloId).SingleOrDefaultAsync(ct);
        var titulo = tituloId is null ? null : await CarregarAsync(db, empresa, tituloId.Value, ct);
        if (titulo is null) return Results.NotFound();
        var parcela = titulo.Parcelas.Single(p => p.Id == parcelaId);
        var hoje = CalendarioBrasilia.Hoje(relogio);
        Baixa baixa;
        try
        {
            baixa = parcela.Estornar(id, usuario.Nome, relogio.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }
        db.Movimentos.Add(MovimentoFinanceiro.DoEstorno(titulo, parcela, baixa, hoje, relogio.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("baixa_estornada",
            $"Baixa de R$ {Titulo.Moeda(baixa.Total)} de {titulo.Descricao} ({parcela.Numero}/{titulo.Parcelas.Count}) estornada", ct: ct);
        return Results.Ok(await ParaDtoAsync(db, titulo, hoje, ct));
    }
}
