using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Endpoints;

/// <summary>Contas financeiras da empresa ativa (saldo = inicial + movimentos), extrato e transferências entre contas.</summary>
internal static class ContasEndpoints
{
    public sealed record ContaRequest(string? Nome, string? Tipo, decimal SaldoInicial, DateOnly? SaldoInicialEm);
    public sealed record ContaDto(Guid Id, Guid EmpresaId, string Nome, string Tipo, decimal SaldoInicial, DateOnly SaldoInicialEm, decimal Saldo, bool Ativa);
    public sealed record MovimentoDto(Guid Id, DateOnly Data, string Descricao, decimal Valor, decimal Saldo, string Origem, Guid? TituloId, string? Usuario);
    public sealed record ExtratoDto(Guid ContaId, string Conta, DateOnly De, DateOnly Ate, decimal SaldoAnterior, IReadOnlyList<MovimentoDto> Movimentos, decimal SaldoFinal);
    public sealed record TransferenciaRequest(Guid OrigemId, Guid DestinoId, DateOnly? Data, decimal Valor, string? Observacao);

    public static IEndpointRouteBuilder MapContas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/financeiro").RequireAuthorization().WithTags("Financeiro");
        g.MapGet("/contas", Listar).WithSummary("Contas financeiras da empresa ativa com o saldo atual");
        g.MapPost("/contas", Criar).WithSummary("Nova conta (caixa, banco ou carteira) com saldo inicial").Produces<ContaDto>(StatusCodes.Status201Created);
        g.MapPut("/contas/{id:guid}", Atualizar).WithSummary("Altera nome, tipo e saldo inicial");
        g.MapPost("/contas/{id:guid}/desativar", Desativar).WithSummary("Desativa a conta (precisa estar com saldo zero)");
        g.MapPost("/contas/{id:guid}/reativar", Reativar).WithSummary("Reativa a conta");
        g.MapGet("/contas/{id:guid}/extrato", Extrato).WithSummary("Movimentos da conta no período com saldo linha a linha (padrão: mês atual)");
        g.MapPost("/transferencias", Transferir).WithSummary("Transfere entre contas da mesma empresa (ex.: depósito do caixa no banco)");
        return app;
    }

    internal static async Task<List<ContaFinanceira>> VisiveisAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        return await db.Contas.Where(c => ids.Contains(c.EmpresaId)).OrderBy(c => c.Nome).ToListAsync(ct);
    }

    internal static async Task<Dictionary<Guid, decimal>> SaldosAsync(FinanceiroDbContext db, IReadOnlyCollection<ContaFinanceira> contas, DateOnly? ate, CancellationToken ct)
    {
        var ids = contas.Select(c => c.Id).ToList();
        var movimentos = await db.Movimentos.AsNoTracking().Where(m => ids.Contains(m.ContaId) && (ate == null || m.Data <= ate))
            .GroupBy(m => m.ContaId).Select(g => new { g.Key, Total = g.Sum(m => m.Valor) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return contas.ToDictionary(c => c.Id, c => c.SaldoInicial + movimentos.GetValueOrDefault(c.Id));
    }

    private static async Task<ContaDto> ParaDtoAsync(FinanceiroDbContext db, ContaFinanceira c, CancellationToken ct) =>
        Dto(c, (await SaldosAsync(db, [c], null, ct))[c.Id]);

    private static ContaDto Dto(ContaFinanceira c, decimal saldo) =>
        new(c.Id, c.EmpresaId, c.Nome, c.Tipo.ToString(), c.SaldoInicial, c.SaldoInicialEm, saldo, c.Ativa);

    private static async Task<IResult> Listar(FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var contas = await VisiveisAsync(db, empresa, ct);
        var saldos = await SaldosAsync(db, contas, null, ct);
        return Results.Ok(contas.Select(c => Dto(c, saldos[c.Id])).ToList());
    }

    private static bool TentarTipo(string? texto, out TipoDeConta tipo) =>
        Enum.TryParse(texto, ignoreCase: true, out tipo) && Enum.IsDefined(tipo);

    private static async Task<IResult?> NomeRepetidoAsync(FinanceiroDbContext db, Guid empresaId, string? nome, Guid? exceto, CancellationToken ct)
    {
        var chave = TextoParaBusca.Normalizar(nome);
        var nomes = await db.Contas.Where(c => c.EmpresaId == empresaId && c.Id != exceto).Select(c => c.Nome).ToListAsync(ct);
        return nomes.Any(n => TextoParaBusca.Normalizar(n) == chave)
            ? Permissoes.Problema(StatusCodes.Status409Conflict, $"Já existe uma conta chamada {nome?.Trim()} nesta empresa.") : null;
    }

    private static async Task<IResult> Criar(ContaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo de conta inválido: use Caixa, Banco ou Carteira.");
        if (await NomeRepetidoAsync(db, empresa.EmpresaId!.Value, r.Nome, null, ct) is { } repetido) return repetido;
        ContaFinanceira conta;
        try
        {
            conta = ContaFinanceira.Criar(empresa.EmpresaId.Value, r.Nome ?? "", tipo, r.SaldoInicial, r.SaldoInicialEm ?? Kamus.SharedKernel.Tempo.CalendarioBrasilia.Hoje(relogio));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        db.Contas.Add(conta);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/financeiro/contas/{conta.Id}", Dto(conta, conta.SaldoInicial));
    }

    private static async Task<ContaFinanceira?> DaEmpresaAtivaAsync(FinanceiroDbContext db, IEmpresaAtiva empresa, Guid id, CancellationToken ct) =>
        await db.Contas.SingleOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresa.EmpresaId, ct);

    private static async Task<IResult> Atualizar(Guid id, ContaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var conta = await DaEmpresaAtivaAsync(db, empresa, id, ct);
        if (conta is null) return Results.NotFound();
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo de conta inválido: use Caixa, Banco ou Carteira.");
        if (await NomeRepetidoAsync(db, conta.EmpresaId, r.Nome, id, ct) is { } repetido) return repetido;
        try
        {
            conta.Atualizar(r.Nome ?? "", tipo, r.SaldoInicial, r.SaldoInicialEm ?? conta.SaldoInicialEm);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(db, conta, ct));
    }

    private static async Task<IResult> Desativar(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var conta = await DaEmpresaAtivaAsync(db, empresa, id, ct);
        if (conta is null) return Results.NotFound();
        var saldo = (await SaldosAsync(db, [conta], null, ct))[conta.Id];
        if (saldo != 0) return Permissoes.Problema(StatusCodes.Status409Conflict, $"{conta.Nome} ainda tem saldo de R$ {Titulo.Moeda(saldo)}. Transfira antes de desativar.");
        conta.Desativar();
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(conta, 0));
    }

    private static async Task<IResult> Reativar(Guid id, FinanceiroDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var conta = await DaEmpresaAtivaAsync(db, empresa, id, ct);
        if (conta is null) return Results.NotFound();
        conta.Reativar();
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(db, conta, ct));
    }

    private static async Task<IResult> Extrato(Guid id, DateOnly? de, DateOnly? ate, FinanceiroDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var conta = (await VisiveisAsync(db, empresa, ct)).FirstOrDefault(c => c.Id == id);
        if (conta is null) return Results.NotFound();
        var hoje = Kamus.SharedKernel.Tempo.CalendarioBrasilia.Hoje(relogio);
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = ate ?? inicio.AddMonths(1).AddDays(-1);
        if (fim < inicio) return Permissoes.Erro("A data final precisa ser depois da inicial.");

        var anterior = (await SaldosAsync(db, [conta], inicio.AddDays(-1), ct))[conta.Id];
        var movimentos = await db.Movimentos.AsNoTracking().Where(m => m.ContaId == id && m.Data >= inicio && m.Data <= fim)
            .OrderBy(m => m.Data).ThenBy(m => m.RegistradoEm).ToListAsync(ct);
        var saldo = anterior;
        var linhas = movimentos.Select(m =>
        {
            saldo += m.Valor;
            return new MovimentoDto(m.Id, m.Data, m.Descricao, m.Valor, saldo, m.Origem.ToString(), m.TituloId, m.Usuario);
        }).ToList();
        return Results.Ok(new ExtratoDto(conta.Id, conta.Nome, inicio, fim, anterior, linhas, saldo));
    }

    private static async Task<IResult> Transferir(TransferenciaRequest r, FinanceiroDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Barrar(empresa) is { } negado) return negado;
        var origem = await DaEmpresaAtivaAsync(db, empresa, r.OrigemId, ct);
        var destino = await DaEmpresaAtivaAsync(db, empresa, r.DestinoId, ct);
        if (origem is null || destino is null) return Results.NotFound();
        if (!origem.Ativa || !destino.Ativa) return Permissoes.Erro("As duas contas precisam estar ativas.");
        try
        {
            var (saida, entrada) = MovimentoFinanceiro.Transferencia(origem, destino, r.Data ?? Kamus.SharedKernel.Tempo.CalendarioBrasilia.Hoje(relogio),
                r.Valor, r.Observacao, usuario.Nome, relogio.GetUtcNow());
            db.Movimentos.AddRange(saida, entrada);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
