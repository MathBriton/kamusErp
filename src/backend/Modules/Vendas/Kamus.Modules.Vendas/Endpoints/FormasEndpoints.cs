using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Formas de pagamento (taxa, prazo e conta de entrada) e regras de venda da empresa ativa.</summary>
internal static class FormasEndpoints
{
    public sealed record FormaRequest(string? Nome, string? Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    public sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias, bool Ativa);
    public sealed record ConfiguracaoDto(bool PermitirEstoqueNegativo);

    private const string SoGestao = "Só Dono e Gerente configuram as vendas.";

    public static IEndpointRouteBuilder MapFormas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/formas", Listar).WithSummary("Formas de pagamento da empresa ativa (todos os perfis veem)");
        g.MapPost("/formas/padrao", Padrao).WithSummary("Cria as formas padrão que ainda não existem (dinheiro, PIX, débito e crédito)");
        g.MapPost("/formas", Criar).WithSummary("Nova forma de pagamento").Produces<FormaDto>(StatusCodes.Status201Created);
        g.MapPut("/formas/{id:guid}", Atualizar).WithSummary("Altera nome, tipo, conta, taxas, parcelas e prazo");
        g.MapPost("/formas/{id:guid}/desativar", (Guid id, VendasDbContext db, IEmpresaAtiva e, CancellationToken ct) => Alternar(id, false, db, e, ct)).WithSummary("Desativa");
        g.MapPost("/formas/{id:guid}/reativar", (Guid id, VendasDbContext db, IEmpresaAtiva e, CancellationToken ct) => Alternar(id, true, db, e, ct)).WithSummary("Reativa");
        g.MapGet("/configuracao", ObterConfiguracao).WithSummary("Regras de venda da empresa ativa");
        g.MapPut("/configuracao", DefinirConfiguracao).WithSummary("Define se a venda pode deixar o estoque negativo");
        return app;
    }

    private static FormaDto Dto(FormaDePagamento f) => new(f.Id, f.Nome, f.Tipo.ToString(), f.ContaId, f.TaxaAVista, f.TaxaParcelado, f.MaxParcelas, f.PrazoDias, f.Ativa);

    private static async Task<IResult> Listar(VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var formas = await db.Formas.AsNoTracking().Where(f => ids.Contains(f.EmpresaId)).ToListAsync(ct);
        return Results.Ok(formas.OrderByDescending(f => f.Ativa).ThenBy(f => f.Tipo).ThenBy(f => f.Nome, StringComparer.CurrentCulture).Select(Dto).ToList());
    }

    private static async Task<IResult> Padrao(VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, SoGestao) is { } negado) return negado;
        var empresaId = empresa.EmpresaId!.Value;
        var existentes = (await db.Formas.Where(f => f.EmpresaId == empresaId).Select(f => f.Nome).ToListAsync(ct)).Select(TextoParaBusca.Normalizar).ToHashSet();
        foreach (var p in FormaDePagamento.Padroes.Where(p => !existentes.Contains(TextoParaBusca.Normalizar(p.Nome))))
            db.Formas.Add(FormaDePagamento.Criar(empresaId, p.Nome, p.Tipo, null, p.TaxaAVista, p.TaxaParcelado, p.MaxParcelas, p.PrazoDias));
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }

    private static bool TentarTipo(string? texto, out TipoDePagamento tipo) => Enum.TryParse(texto, ignoreCase: true, out tipo) && Enum.IsDefined(tipo);

    private static async Task<IResult?> ValidarAsync(FormaRequest r, Guid empresaId, Guid? exceto, VendasDbContext db, IContasAReceber financeiro, CancellationToken ct)
    {
        var chave = TextoParaBusca.Normalizar(r.Nome);
        if ((await db.Formas.Where(f => f.EmpresaId == empresaId && f.Id != exceto).Select(f => f.Nome).ToListAsync(ct)).Any(n => TextoParaBusca.Normalizar(n) == chave))
            return Permissoes.Problema(StatusCodes.Status409Conflict, $"Já existe a forma de pagamento {r.Nome?.Trim()}.");
        if (r.ContaId is { } conta && !await financeiro.ContaValidaAsync(empresaId, conta, ct))
            return Permissoes.Erro("Escolha uma conta financeira ativa desta empresa.");
        return null;
    }

    private static async Task<IResult> Criar(FormaRequest r, VendasDbContext db, IEmpresaAtiva empresa, IContasAReceber financeiro, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, SoGestao) is { } negado) return negado;
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo inválido: Dinheiro, Pix, Debito, Credito ou Outro.");
        var empresaId = empresa.EmpresaId!.Value;
        if (await ValidarAsync(r, empresaId, null, db, financeiro, ct) is { } invalida) return invalida;
        try
        {
            var forma = FormaDePagamento.Criar(empresaId, r.Nome ?? "", tipo, r.ContaId, r.TaxaAVista, r.TaxaParcelado, r.MaxParcelas, r.PrazoDias);
            db.Formas.Add(forma);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/vendas/formas/{forma.Id}", Dto(forma));
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
    }

    private static async Task<IResult> Atualizar(Guid id, FormaRequest r, VendasDbContext db, IEmpresaAtiva empresa, IContasAReceber financeiro, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, SoGestao) is { } negado) return negado;
        var forma = await db.Formas.SingleOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresa.EmpresaId, ct);
        if (forma is null) return Results.NotFound();
        if (!TentarTipo(r.Tipo, out var tipo)) return Permissoes.Erro("Tipo inválido: Dinheiro, Pix, Debito, Credito ou Outro.");
        if (await ValidarAsync(r, forma.EmpresaId, id, db, financeiro, ct) is { } invalida) return invalida;
        try
        {
            forma.Atualizar(r.Nome ?? "", tipo, r.ContaId, r.TaxaAVista, r.TaxaParcelado, r.MaxParcelas, r.PrazoDias);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(forma));
    }

    private static async Task<IResult> Alternar(Guid id, bool ativa, VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, SoGestao) is { } negado) return negado;
        var forma = await db.Formas.SingleOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresa.EmpresaId, ct);
        if (forma is null) return Results.NotFound();
        if (ativa) forma.Reativar();
        else forma.Desativar();
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(forma));
    }

    internal static async Task<ConfiguracaoDeVendas> ConfiguracaoAsync(VendasDbContext db, Guid empresaId, CancellationToken ct) =>
        await db.Configuracoes.SingleOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoDeVendas.Padrao(empresaId);

    private static async Task<IResult> ObterConfiguracao(VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (empresa.EmpresaId is not { } empresaId) return Permissoes.Erro("Escolha uma empresa.");
        return Results.Ok(new ConfiguracaoDto((await ConfiguracaoAsync(db, empresaId, ct)).PermitirEstoqueNegativo));
    }

    private static async Task<IResult> DefinirConfiguracao(ConfiguracaoDto r, VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, SoGestao) is { } negado) return negado;
        var empresaId = empresa.EmpresaId!.Value;
        var configuracao = await db.Configuracoes.SingleOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (configuracao is null)
        {
            configuracao = ConfiguracaoDeVendas.Padrao(empresaId);
            db.Configuracoes.Add(configuracao);
        }
        configuracao.Definir(r.PermitirEstoqueNegativo);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new ConfiguracaoDto(configuracao.PermitirEstoqueNegativo));
    }
}
