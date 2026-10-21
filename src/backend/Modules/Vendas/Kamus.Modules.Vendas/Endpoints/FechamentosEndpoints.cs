using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Fechamento de comissão por vendedor e período, com conta a pagar opcional; reabrir cancela a conta (se ainda não paga).</summary>
internal static class FechamentosEndpoints
{
    public const string Origem = "FechamentoDeComissao";

    public sealed record FechamentoRequest(Guid VendedorId, DateOnly De, DateOnly Ate, bool GerarContaAPagar, DateOnly? Vencimento);
    public sealed record FechamentoDto(Guid Id, Guid VendedorId, string VendedorNome, DateOnly De, DateOnly Ate, int Vendas, int Trocas, decimal TotalVendido,
        decimal Comissao, string? FechadoPor, DateTimeOffset FechadoEm, bool ContaAPagarGerada);

    public static IEndpointRouteBuilder MapFechamentos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas/comissoes/fechamentos").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/", Listar).WithSummary("Fechamentos de comissão (vendedorId opcional; pagina e tamanho devolvem { itens, total, pagina, tamanho })");
        g.MapPost("/", Fechar).WithSummary("Fecha a comissão do vendedor no período (vendas e trocas ainda abertas) e gera a conta a pagar, se pedido")
            .Produces<FechamentoDto>(StatusCodes.Status201Created);
        g.MapPost("/{id:guid}/reabrir", Reabrir).WithSummary("Reabre: solta as vendas/trocas e cancela a conta a pagar (se ainda não paga)");
        return app;
    }

    private static FechamentoDto Dto(FechamentoDeComissao f) => new(f.Id, f.VendedorId, f.VendedorNome, f.De, f.Ate, f.Vendas, f.Trocas, f.TotalVendido, f.Comissao,
        f.FechadoPor, f.FechadoEm, f.ContaAPagarGerada);

    /// <param name="selecionados">Só estes fechamentos (linhas marcadas na grade).</param>
    internal static async Task<List<FechamentoDto>> ListaAsync(VendasDbContext db, IEmpresaAtiva empresa, Guid? vendedorId, CancellationToken ct,
        IReadOnlySet<Guid>? selecionados = null)
    {
        var ids = empresa.EmpresaIds;
        var q = db.Fechamentos.AsNoTracking().Where(f => ids.Contains(f.EmpresaId));
        if (vendedorId is { } v) q = q.Where(f => f.VendedorId == v);
        if (selecionados is { Count: > 0 })
        {
            var lista = selecionados.ToList();
            q = q.Where(f => lista.Contains(f.Id));
        }
        return (await q.ToListAsync(ct)).OrderByDescending(f => f.Ate).ThenBy(f => f.VendedorNome, StringComparer.CurrentCulture).Select(Dto).ToList();
    }

    private static async Task<IResult> Listar(Guid? vendedorId, int? pagina, int? tamanho, VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Consultar, "Só Dono, Gerente e Financeiro veem as comissões.") is { } negado) return negado;
        var lista = await ListaAsync(db, empresa, vendedorId, ct);
        return pagina is null ? Results.Ok(lista) : Results.Ok(lista.PaginarSePedido(pagina, tamanho));
    }

    private static async Task<IResult> Fechar(FechamentoRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, IContasAPagar contas,
        IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, "Só Dono e Gerente fecham comissões.") is { } negado) return negado;
        var empresaId = empresa.EmpresaId!.Value;
        var vendas = await db.Vendas.Include(v => v.Itens).Include(v => v.Pagamentos)
            .Where(v => v.EmpresaId == empresaId && v.VendedorId == r.VendedorId && v.Status == StatusDaVenda.Concluida && v.FechamentoId == null
                && v.Data >= r.De && v.Data <= r.Ate).ToListAsync(ct);
        var trocas = await db.Trocas.Include(t => t.Devolvidos).Include(t => t.Levados).Include(t => t.Pagamentos)
            .Where(t => t.EmpresaId == empresaId && t.VendedorId == r.VendedorId && t.FechamentoId == null && t.Data >= r.De && t.Data <= r.Ate).ToListAsync(ct);
        var nome = vendas.OrderBy(v => v.Numero).LastOrDefault()?.VendedorNome ?? trocas.FirstOrDefault()?.VendedorNome ?? "Vendedor";

        FechamentoDeComissao fechamento;
        try
        {
            fechamento = FechamentoDeComissao.Fechar(empresaId, r.VendedorId, nome, r.De, r.Ate, vendas, trocas, usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        db.Fechamentos.Add(fechamento);
        await db.SaveChangesAsync(ct);

        if (r.GerarContaAPagar && fechamento.Comissao > 0)
        {
            await contas.RegistrarContaAsync(new NovaContaAPagar(empresaId, Origem, fechamento.Id.ToString(),
                $"Comissão de {nome} — {r.De:dd/MM/yyyy} a {r.Ate:dd/MM/yyyy}", nome, "Pessoal", "Comissões", r.Vencimento ?? CalendarioBrasilia.Hoje(relogio),
                fechamento.Comissao), ct);
            fechamento.RegistrarContaAPagar();
            await db.SaveChangesAsync(ct);
        }
        await eventos.RegistrarAsync("comissao_fechada", $"Comissão de {nome} de {r.De:dd/MM/yyyy} a {r.Ate:dd/MM/yyyy} fechada: R$ {Dinheiro.Formatar(fechamento.Comissao)}", ct: ct);
        return Results.Created($"/api/vendas/comissoes/fechamentos/{fechamento.Id}", Dto(fechamento));
    }

    private static async Task<IResult> Reabrir(Guid id, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, IContasAPagar contas,
        IRegistradorDeEventos eventos, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, "Só Dono e Gerente reabrem comissões.") is { } negado) return negado;
        var fechamento = await db.Fechamentos.SingleOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresa.EmpresaId, ct);
        if (fechamento is null) return Results.NotFound();
        if (fechamento.ContaAPagarGerada && !await contas.CancelarOrigemAsync(Origem, id.ToString(), usuario.Nome, ct))
            return Permissoes.Problema(StatusCodes.Status409Conflict, "A comissão já foi paga no financeiro: estorne o pagamento antes de reabrir.");

        FechamentoDeComissao.Reabrir(await db.Vendas.Where(v => v.FechamentoId == id).ToListAsync(ct), await db.Trocas.Where(t => t.FechamentoId == id).ToListAsync(ct));
        db.Fechamentos.Remove(fechamento);
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("comissao_reaberta", $"Fechamento de comissão de {fechamento.VendedorNome} ({fechamento.De:dd/MM/yyyy} a {fechamento.Ate:dd/MM/yyyy}) reaberto", ct: ct);
        return Results.Ok();
    }
}
