using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>
/// Inventário (contagem física): abrir tira a foto do saldo; Dono, Gerente e Estoquista contam (matriz ou leitor, sem ver o saldo);
/// Dono ou Gerente concluem, gerando ajustes pela diferença. Um inventário aberto por loja.
/// </summary>
internal static class InventariosEndpoints
{
    private static readonly string[] Aprovadores = ["Dono", "Gerente"];

    public sealed record AbrirRequest(Guid LojaId, string? Observacao);
    public sealed record ContagemRequest(Guid SkuId, int? Quantidade);
    public sealed record ContagensRequest(IReadOnlyList<ContagemRequest>? Itens);
    public sealed record LeituraRequest(string? Codigo, int? Quantidade);
    public sealed record ConcluirRequest(bool ZerarNaoContados);

    public sealed record InventarioDto(Guid Id, Guid LojaId, string Loja, string Status, string? Observacao, DateTimeOffset AbertoEm, string? AbertoPor,
        DateTimeOffset? EncerradoEm, string? ConcluidoPor, int SkusContados, int PecasContadas, Guid? OperacaoId);
    public sealed record CelulaDto(Guid SkuId, Guid CorId, string Tamanho, bool Ativo, int? Contado);
    public sealed record ContagemDto(Guid ProdutoId, string Referencia, string Descricao, IReadOnlyList<string> Tamanhos, IReadOnlyList<CorDoCatalogo> Cores,
        IReadOnlyList<CelulaDto> Celulas);
    public sealed record LeituraDto(Guid SkuId, string Referencia, string Descricao, string Cor, string Tamanho, int Contado);
    public sealed record DivergenciaDto(Guid SkuId, Guid ProdutoId, string Referencia, string Descricao, string Cor, string Tamanho, int SaldoNaAbertura,
        int? Contado, int Diferenca, decimal? ValorDaDiferenca);
    public sealed record DivergenciasDto(IReadOnlyList<DivergenciaDto> Itens, int Sobras, int Faltas, decimal? Valor, int NaoContados);
    public sealed record ConclusaoDto(Guid OperacaoId, int Ajustes, int Sobras, int Faltas);

    public static IEndpointRouteBuilder MapInventarios(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque/inventarios").RequireAuthorization().WithTags("Estoque");
        g.MapGet("/", Listar).WithSummary("Inventários das lojas visíveis (mais recentes primeiro)");
        g.MapPost("/", Abrir).WithSummary("Abre o inventário da loja e tira a foto do saldo (um aberto por loja)").Produces<InventarioDto>(StatusCodes.Status201Created);
        g.MapGet("/{id:guid}", Obter).WithSummary("Situação do inventário");
        g.MapGet("/{id:guid}/produtos/{produtoId:guid}", Contagem).WithSummary("Grade do produto com as quantidades já contadas (sem o saldo: contagem cega)");
        g.MapPut("/{id:guid}/contagens", Contar).WithSummary("Define as quantidades contadas por SKU (quantidade nula limpa a contagem)");
        g.MapPost("/{id:guid}/leituras", Ler).WithSummary("Leitor de código de barras: soma 1 (ou a quantidade) ao SKU lido");
        g.MapGet("/{id:guid}/divergencias", Divergencias).WithSummary("Diferenças entre o contado e a foto (zerarNaoContados=true trata os não contados como zero)");
        g.MapPost("/{id:guid}/concluir", Concluir).WithSummary("Conclui (Dono ou Gerente): ajusta o estoque pela diferença");
        g.MapPost("/{id:guid}/cancelar", Cancelar).WithSummary("Cancela sem ajustar nada (Dono ou Gerente)");
        return app;
    }

    private static IResult Conflito(string detalhe) => Permissoes.Problema(StatusCodes.Status409Conflict, detalhe);

    private static IResult? Barrar(IEmpresaAtiva empresa, bool aprovar = false) =>
        aprovar
            ? Aprovadores.Contains(empresa.Perfil) ? null : Permissoes.Negado("Só Dono e Gerente concluem ou cancelam inventários.")
            : Permissoes.PodeMovimentar(empresa.Perfil) ? null : Permissoes.Negado("Só Dono, Gerente e Estoquista fazem inventário.");

    /// <summary>Inventário de uma loja visível (com os itens) ou nulo.</summary>
    private static async Task<(Inventario? Inventario, Loja? Loja)> CarregarAsync(EstoqueDbContext db, Lojas lojas, Guid id, CancellationToken ct)
    {
        var inventario = await db.Inventarios.Include(i => i.Itens).SingleOrDefaultAsync(i => i.Id == id, ct);
        var loja = inventario is null ? null : (await lojas.VisiveisAsync(ct)).FirstOrDefault(l => l.Id == inventario.LojaId);
        return loja is null ? (null, null) : (inventario, loja);
    }

    internal static InventarioDto ParaDto(Inventario i, string loja)
    {
        var contados = i.Itens.Where(x => x.Contado is not null).ToList();
        return new InventarioDto(i.Id, i.LojaId, loja, i.Status.ToString(), i.Observacao, i.AbertoEm, i.AbertoPor, i.EncerradoEm, i.ConcluidoPor,
            contados.Count, contados.Sum(x => x.Contado!.Value), i.OperacaoId);
    }

    private static async Task<IResult> Listar(Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var nomes = (await lojas.VisiveisAsync(ct)).ToDictionary(l => l.Id, l => l.Nome);
        var ids = nomes.Keys.ToList();
        var inventarios = await db.Inventarios.AsNoTracking().Include(i => i.Itens).Where(i => ids.Contains(i.LojaId))
            .OrderByDescending(i => i.AbertoEm).Take(100).ToListAsync(ct);
        return Results.Ok(inventarios.Select(i => ParaDto(i, nomes[i.LojaId])).ToList());
    }

    private static async Task<IResult> Abrir(AbrirRequest r, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var loja = await lojas.DaEmpresaAtivaAsync(r.LojaId, ct);
        if (loja is null) return Results.NotFound();
        if (!loja.Ativa) return Permissoes.Problema(StatusCodes.Status400BadRequest, $"{loja.Nome} está desativada.");
        if (await db.Inventarios.AnyAsync(i => i.LojaId == loja.Id && i.Status == StatusDoInventario.Aberto, ct))
            return Conflito($"Já existe um inventário aberto em {loja.Nome}. Conclua ou cancele antes de abrir outro.");

        var foto = await db.Saldos.AsNoTracking().Where(s => s.LojaId == loja.Id)
            .Select(s => new FotoDoSaldo(s.SkuId, s.ProdutoId, s.Quantidade)).ToListAsync(ct);
        var inventario = Inventario.Abrir(loja.Id, foto, relogio.GetUtcNow(), usuario.Nome, r.Observacao);
        db.Inventarios.Add(inventario);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/estoque/inventarios/{inventario.Id}", ParaDto(inventario, loja.Nome));
    }

    private static async Task<IResult> Obter(Guid id, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var (inventario, loja) = await CarregarAsync(db, lojas, id, ct);
        return inventario is null ? Results.NotFound() : Results.Ok(ParaDto(inventario, loja!.Nome));
    }

    private static async Task<IResult> Contagem(Guid id, Guid produtoId, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var (inventario, _) = await CarregarAsync(db, lojas, id, ct);
        var produto = inventario is null ? null : await catalogo.ProdutoAsync(produtoId, ct);
        if (produto is null) return Results.NotFound();
        var contados = inventario!.Itens.ToDictionary(i => i.SkuId, i => i.Contado);
        return Results.Ok(new ContagemDto(produto.Id, produto.Referencia, produto.Descricao, produto.Tamanhos, produto.Cores,
            produto.Skus.Select(s => new CelulaDto(s.Id, s.CorId, s.Tamanho, s.Ativo, contados.GetValueOrDefault(s.Id))).ToList()));
    }

    private static async Task<IResult> Contar(Guid id, ContagensRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var (inventario, _) = await CarregarAsync(db, lojas, id, ct);
        if (inventario is null) return Results.NotFound();
        if (inventario.Status != StatusDoInventario.Aberto) return Conflito("Este inventário já foi encerrado.");
        var itens = r.Itens ?? [];
        if (itens.Count == 0) return Permissoes.Problema(StatusCodes.Status400BadRequest, "Informe ao menos uma contagem.");
        if (itens.Any(i => i.Quantidade < 0)) return Permissoes.Problema(StatusCodes.Status400BadRequest, "A quantidade contada não pode ser negativa.");
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (itens.Any(i => !skus.ContainsKey(i.SkuId))) return Permissoes.Problema(StatusCodes.Status400BadRequest, "Há itens que não existem no catálogo.");

        var agora = relogio.GetUtcNow();
        foreach (var item in itens)
        {
            if (item.Quantidade is null) inventario.LimparContagem(item.SkuId);
            else AdicionarSeNovo(db, inventario.Contar(item.SkuId, skus[item.SkuId].ProdutoId, item.Quantidade.Value, somar: false, agora));
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(ParaDto(inventario, ""));
    }

    // Itens novos na coleção precisam ser marcados como adicionados (o EF não deduz pela chave já preenchida).
    private static void AdicionarSeNovo(EstoqueDbContext db, ItemDeInventario item)
    {
        if (db.Entry(item).State == EntityState.Detached) db.ItensDeInventario.Add(item);
    }

    private static async Task<IResult> Ler(Guid id, LeituraRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var (inventario, _) = await CarregarAsync(db, lojas, id, ct);
        if (inventario is null) return Results.NotFound();
        if (inventario.Status != StatusDoInventario.Aberto) return Conflito("Este inventário já foi encerrado.");
        var sku = string.IsNullOrWhiteSpace(r.Codigo) ? null : await catalogo.SkuPorCodigoAsync(r.Codigo, ct);
        if (sku is null) return Permissoes.Problema(StatusCodes.Status404NotFound, $"Código {r.Codigo} não encontrado no catálogo.");
        try
        {
            var item = inventario.Contar(sku.Id, sku.ProdutoId, r.Quantidade ?? 1, somar: true, relogio.GetUtcNow());
            AdicionarSeNovo(db, item);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new LeituraDto(sku.Id, sku.Referencia, sku.Descricao, sku.Cor, sku.Tamanho, item.Contado!.Value));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Permissoes.Problema(StatusCodes.Status400BadRequest, "A contagem não pode ficar negativa.");
        }
    }

    internal static async Task<DivergenciasDto> CalcularAsync(Inventario inventario, bool zerarNaoContados, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        bool verCusto, CancellationToken ct)
    {
        var diferencas = inventario.Diferencas(zerarNaoContados).ToDictionary(d => d.SkuId, d => d.Diferenca);
        var itens = inventario.Itens.Where(i => diferencas.ContainsKey(i.SkuId)).ToList();
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).ToList(), ct)).ToDictionary(s => s.Id);
        var ids = itens.Select(i => i.SkuId).ToList();
        var custos = await db.Saldos.AsNoTracking().Where(s => s.LojaId == inventario.LojaId && ids.Contains(s.SkuId)).ToDictionaryAsync(s => s.SkuId, s => s.CustoMedio, ct);

        var linhas = itens.Where(i => skus.ContainsKey(i.SkuId)).Select(i =>
        {
            var sku = skus[i.SkuId];
            var custo = custos.TryGetValue(i.SkuId, out var c) && c > 0 ? c : sku.PrecoCusto ?? 0;
            return new DivergenciaDto(i.SkuId, i.ProdutoId, sku.Referencia, sku.Descricao, sku.Cor, sku.Tamanho, i.SaldoNaAbertura, i.Contado,
                diferencas[i.SkuId], verCusto ? Math.Round(diferencas[i.SkuId] * custo, 2) : null);
        }).OrderBy(d => d.Descricao, StringComparer.CurrentCulture).ThenBy(d => d.Cor, StringComparer.CurrentCulture).ThenBy(d => skus[d.SkuId].OrdemTamanho).ToList();

        var naoContados = inventario.Itens.Count(i => i.Contado is null && i.SaldoNaAbertura != 0);
        return new DivergenciasDto(linhas, linhas.Where(l => l.Diferenca > 0).Sum(l => l.Diferenca), linhas.Where(l => l.Diferenca < 0).Sum(l => -l.Diferenca),
            verCusto ? linhas.Sum(l => l.ValorDaDiferenca ?? 0) : null, naoContados);
    }

    private static async Task<IResult> Divergencias(Guid id, bool? zerarNaoContados, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var (inventario, _) = await CarregarAsync(db, lojas, id, ct);
        if (inventario is null) return Results.NotFound();
        return Results.Ok(await CalcularAsync(inventario, zerarNaoContados ?? false, db, catalogo, Permissoes.PodeVerCusto(empresa.Perfil), ct));
    }

    private static async Task<IResult> Concluir(Guid id, ConcluirRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, IUsuarioAtual usuario, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa, aprovar: true) is { } negado) return negado;
        var (inventario, loja) = await CarregarAsync(db, lojas, id, ct);
        if (inventario is null) return Results.NotFound();
        if (inventario.Status != StatusDoInventario.Aberto) return Conflito("Este inventário já foi encerrado.");

        var diferencas = inventario.Diferencas(r.ZerarNaoContados);
        var skus = (await catalogo.SkusAsync(diferencas.Select(d => d.SkuId).ToList(), ct)).ToDictionary(s => s.Id);
        var saldos = await Saldos.DaLojaAsync(db, inventario.LojaId, skus.Values, ct);
        var agora = relogio.GetUtcNow();
        var op = new Operacao(Guid.NewGuid(), agora, usuario.Id, usuario.Nome, "Inventário", $"Inventário {inventario.AbertoEm:dd/MM/yyyy}", inventario.Observacao, null);
        var movimentos = diferencas.Where(d => skus.ContainsKey(d.SkuId))
            .Select(d => saldos[d.SkuId].Inventariar(d.Diferenca, skus[d.SkuId].PrecoCusto ?? 0, op))
            .OfType<MovimentoDeEstoque>().ToList();
        db.Movimentos.AddRange(movimentos);
        inventario.Concluir(agora, usuario.Nome, op.Id, r.ZerarNaoContados);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflito("O estoque mudou enquanto o inventário era concluído. Tente de novo.");
        }

        var (sobras, faltas) = (movimentos.Where(m => m.Quantidade > 0).Sum(m => m.Quantidade), movimentos.Where(m => m.Quantidade < 0).Sum(m => -m.Quantidade));
        await eventos.RegistrarAsync("inventario_concluido",
            $"Inventário de {loja!.Nome} concluído: {movimentos.Count} SKU(s) ajustados, sobras de {sobras} e faltas de {faltas} peça(s)", ct: ct);
        return Results.Ok(new ConclusaoDto(op.Id, movimentos.Count, sobras, faltas));
    }

    private static async Task<IResult> Cancelar(Guid id, Lojas lojas, EstoqueDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario,
        TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa, aprovar: true) is { } negado) return negado;
        var (inventario, loja) = await CarregarAsync(db, lojas, id, ct);
        if (inventario is null) return Results.NotFound();
        if (inventario.Status != StatusDoInventario.Aberto) return Conflito("Este inventário já foi encerrado.");
        inventario.Cancelar(relogio.GetUtcNow(), usuario.Nome);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ParaDto(inventario, loja!.Nome));
    }
}
