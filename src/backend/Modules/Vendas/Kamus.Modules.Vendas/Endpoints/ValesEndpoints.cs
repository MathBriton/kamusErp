using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Vales-troca: consulta pelo código (no caixa), lista com saldos e cancelamento.</summary>
internal static class ValesEndpoints
{
    public sealed record ValeDto(Guid Id, string Codigo, Guid? ClienteId, string? ClienteNome, decimal Valor, decimal Saldo, DateOnly EmitidoEm, DateOnly ValidoAte,
        string Situacao, Guid? TrocaId, string? MotivoDoCancelamento);
    public sealed record ListaDeValesDto(IReadOnlyList<ValeDto> Itens, decimal SaldoAtivo, decimal SaldoVencido);
    public sealed record CancelarValeRequest(string? Motivo);

    public static readonly string[] Situacoes = ["ativos", "usados", "vencidos", "cancelados", "todos"];

    public static IEndpointRouteBuilder MapVales(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas/vales").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/", Listar).WithSummary("Vales-troca (situacao: ativos, usados, vencidos, cancelados, todos; busca por código ou cliente; clienteId)");
        g.MapGet("/por-codigo/{codigo}", PorCodigo).WithSummary("Vale pelo código (para usar como pagamento)");
        g.MapPost("/{id:guid}/cancelar", Cancelar).WithSummary("Cancela o vale com motivo (Dono/Gerente)");
        return app;
    }

    public static ValeDto Dto(ValeTroca v, DateOnly hoje) => new(v.Id, v.Codigo, v.ClienteId, v.ClienteNome, v.Valor, v.Saldo, v.EmitidoEm, v.ValidoAte,
        v.Situacao(hoje).ToString(), v.TrocaId, v.MotivoDoCancelamento);

    public static async Task<string> CodigoLivreAsync(VendasDbContext db, CancellationToken ct)
    {
        while (true)
        {
            var codigo = ValeTroca.NovoCodigo();
            if (!await db.Vales.AnyAsync(v => v.Codigo == codigo, ct)) return codigo;
        }
    }

    private static readonly string[] Perfis = [.. Permissoes.Registrar, .. Permissoes.Consultar];

    internal static async Task<ListaDeValesDto> ListaAsync(VendasDbContext db, IEmpresaAtiva empresa, string situacao, string? busca, Guid? clienteId, DateOnly hoje,
        CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var q = db.Vales.AsNoTracking().Where(v => ids.Contains(v.EmpresaId));
        if (clienteId is { } c) q = q.Where(v => v.ClienteId == c);
        var todos = await q.ToListAsync(ct);
        var alvo = situacao switch
        {
            "ativos" => SituacaoDoVale.Ativo,
            "usados" => SituacaoDoVale.Usado,
            "vencidos" => SituacaoDoVale.Vencido,
            "cancelados" => SituacaoDoVale.Cancelado,
            _ => (SituacaoDoVale?)null,
        };
        var termo = TextoParaBusca.Normalizar(busca);
        var itens = todos.Where(v => alvo is null || v.Situacao(hoje) == alvo)
            .Where(v => string.IsNullOrEmpty(termo) || v.Codigo.Contains(busca!.Trim(), StringComparison.OrdinalIgnoreCase)
                || TextoParaBusca.Normalizar(v.ClienteNome).Contains(termo, StringComparison.Ordinal))
            .OrderByDescending(v => v.EmitidoEm).ThenBy(v => v.Codigo, StringComparer.Ordinal).Select(v => Dto(v, hoje)).ToList();
        return new ListaDeValesDto(itens, todos.Where(v => v.Situacao(hoje) == SituacaoDoVale.Ativo).Sum(v => v.Saldo),
            todos.Where(v => v.Situacao(hoje) == SituacaoDoVale.Vencido).Sum(v => v.Saldo));
    }

    private static async Task<IResult> Listar(string? situacao, string? busca, Guid? clienteId, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio,
        CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa os vales-troca.") is { } negado) return negado;
        var s = situacao?.ToLowerInvariant() ?? "ativos";
        if (!Situacoes.Contains(s)) return Permissoes.Erro("Situação inválida: ativos, usados, vencidos, cancelados ou todos.");
        return Results.Ok(await ListaAsync(db, empresa, s, busca, clienteId, CalendarioBrasilia.Hoje(relogio), ct));
    }

    private static async Task<IResult> PorCodigo(string codigo, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa os vales-troca.") is { } negado) return negado;
        var ids = empresa.EmpresaIds;
        var chave = codigo.Trim().ToUpperInvariant();
        var vale = await db.Vales.AsNoTracking().SingleOrDefaultAsync(v => v.Codigo == chave && ids.Contains(v.EmpresaId), ct);
        return vale is null ? Permissoes.Problema(StatusCodes.Status404NotFound, $"Vale-troca {chave} não encontrado.") : Results.Ok(Dto(vale, CalendarioBrasilia.Hoje(relogio)));
    }

    private static async Task<IResult> Cancelar(Guid id, CancelarValeRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Gestao, "Só Dono e Gerente cancelam vales-troca.") is { } negado) return negado;
        var vale = await db.Vales.SingleOrDefaultAsync(v => v.Id == id && v.EmpresaId == empresa.EmpresaId, ct);
        if (vale is null) return Results.NotFound();
        try
        {
            vale.Cancelar(r.Motivo ?? "", usuario.Nome);
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
        await eventos.RegistrarAsync("vale_troca_cancelado", $"Vale-troca {vale.Codigo} (saldo R$ {Dinheiro.Formatar(vale.Saldo)}) cancelado: {vale.MotivoDoCancelamento}", ct: ct);
        return Results.Ok(Dto(vale, CalendarioBrasilia.Hoje(relogio)));
    }
}
