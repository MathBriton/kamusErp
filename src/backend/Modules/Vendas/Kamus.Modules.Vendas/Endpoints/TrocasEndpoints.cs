using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Troca vinculada à venda: devolve peças (estoque ou avariadas), leva outras, paga a diferença ou recebe vale-troca.</summary>
internal static class TrocasEndpoints
{
    public const string Origem = "Troca";

    public sealed record DevolucaoRequest(Guid SkuId, int Quantidade, string? Destino);
    public sealed record TrocaRequest(Guid VendaId, string? Motivo, string? Observacao, bool ForaDoPrazo, IReadOnlyList<DevolucaoRequest>? Devolvidos,
        IReadOnlyList<VendasEndpoints.ItemRequest>? Levados, IReadOnlyList<PagamentoPedido>? Pagamentos);

    public sealed record DevolvivelDto(Guid SkuId, string Descricao, int Vendidas, int JaDevolvidas, int Disponiveis, decimal ValorPorPeca);
    public sealed record DevolviveisDto(Guid VendaId, Guid LojaId, int Numero, DateOnly Data, string? ClienteNome, string? VendedorNome, int DiasDesdeAVenda, int PrazoDeTrocaDias,
        bool DentroDoPrazo, IReadOnlyList<DevolvivelDto> Itens);

    public sealed record ItemDevolvidoDto(Guid SkuId, string Descricao, int Quantidade, decimal Valor, string Destino);
    public sealed record ItemLevadoDto(Guid SkuId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Desconto, decimal Total);
    public sealed record TrocaDto(Guid Id, int Numero, DateOnly Data, Guid VendaId, int VendaNumero, string? ClienteNome, string? VendedorNome, string Motivo,
        string? Observacao, decimal ValorDevolvido, decimal ValorLevado, decimal Diferenca, decimal Troco, decimal Comissao, string? RegistradaPor,
        IReadOnlyList<ItemDevolvidoDto> Devolvidos, IReadOnlyList<ItemLevadoDto> Levados, IReadOnlyList<VendasEndpoints.PagamentoDto> Pagamentos,
        ValesEndpoints.ValeDto? Vale, IReadOnlyList<string> Avisos);
    public sealed record LinhaDeTroca(Guid Id, int Numero, DateOnly Data, int VendaNumero, string? ClienteNome, string Motivo, int PecasDevolvidas, decimal ValorDevolvido,
        decimal ValorLevado, decimal Diferenca, string? ValeCodigo);

    public static IEndpointRouteBuilder MapTrocas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/{id:guid}/devolviveis", (Guid id, VendasDbContext db, IEmpresaAtiva e, TimeProvider r, CancellationToken ct) => Devolviveis(id, null, db, e, r, ct))
            .WithSummary("Peças da venda que ainda podem ser devolvidas, com o valor pago por peça e o prazo de troca");
        g.MapGet("/devolviveis", (int numero, VendasDbContext db, IEmpresaAtiva e, TimeProvider r, CancellationToken ct) => Devolviveis(null, numero, db, e, r, ct))
            .WithSummary("O mesmo, pelo nº da venda (qualquer venda da empresa, para quem registra trocas)");
        g.MapGet("/trocas", Listar).WithSummary("Trocas no período (de, ate, busca, vendaId; pagina e tamanho devolvem { itens, total, pagina, tamanho })");
        g.MapGet("/trocas/{id:guid}", Obter).WithSummary("Troca com peças devolvidas, levadas, pagamentos e vale");
        g.MapPost("/trocas", Registrar).WithSummary("Registra a troca: devolve ao estoque, baixa as peças levadas e lança a diferença ou emite o vale-troca")
            .Produces<TrocaDto>(StatusCodes.Status201Created);
        return app;
    }

    private static readonly string[] Perfis = [.. Permissoes.Registrar, .. Permissoes.Consultar];

    private static async Task<Dictionary<Guid, int>> JaDevolvidasAsync(VendasDbContext db, Guid vendaId, CancellationToken ct) =>
        (await db.Trocas.Where(t => t.VendaId == vendaId).SelectMany(t => t.Devolvidos).Select(d => new { d.SkuId, d.Quantidade }).ToListAsync(ct))
            .GroupBy(d => d.SkuId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantidade));

    private static async Task<IResult> Devolviveis(Guid? id, int? numero, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as vendas.") is { } negado) return negado;
        var ids = empresa.EmpresaIds;
        var venda = await db.Vendas.AsNoTracking().Include(v => v.Itens)
            .Where(v => ids.Contains(v.EmpresaId) && (id == null || v.Id == id) && (numero == null || v.Numero == numero))
            .OrderByDescending(v => v.EmpresaId == empresa.EmpresaId).FirstOrDefaultAsync(ct);
        if (venda is null)
            return Permissoes.Problema(StatusCodes.Status404NotFound, numero is null ? "Venda não encontrada." : $"Venda nº {numero} não encontrada nesta empresa.");
        if (venda.Status == StatusDaVenda.Cancelada) return Permissoes.Erro($"A venda nº {venda.Numero} foi cancelada: não há o que trocar.");
        var ja = await JaDevolvidasAsync(db, venda.Id, ct);
        var prazo = (await FormasEndpoints.ConfiguracaoAsync(db, venda.EmpresaId, ct)).PrazoDeTrocaDias;
        var dias = CalendarioBrasilia.Hoje(relogio).DayNumber - venda.Data.DayNumber;
        var itens = venda.ValoresPagosPorSku().Select(x =>
        {
            var item = venda.Itens.First(i => i.SkuId == x.Key);
            var jaDevolvidas = ja.GetValueOrDefault(x.Key);
            return new DevolvivelDto(x.Key, item.Descricao, x.Value.Quantidade, jaDevolvidas, x.Value.Quantidade - jaDevolvidas, Dinheiro.Arredondar(x.Value.ValorPago / x.Value.Quantidade));
        }).OrderBy(i => i.Descricao, StringComparer.CurrentCulture).ToList();
        return Results.Ok(new DevolviveisDto(venda.Id, venda.LojaId, venda.Numero, venda.Data, venda.ClienteNome, venda.VendedorNome, dias, prazo,
            venda.Status == StatusDaVenda.Concluida && dias <= prazo, itens));
    }

    private static IQueryable<Troca> Visiveis(VendasDbContext db, IEmpresaAtiva empresa, Guid? usuarioId)
    {
        var ids = empresa.EmpresaIds;
        var trocas = db.Trocas.AsNoTracking().Include(t => t.Devolvidos).Include(t => t.Levados).Include(t => t.Pagamentos).Where(t => ids.Contains(t.EmpresaId));
        return Permissoes.VeTudo(empresa) ? trocas : trocas.Where(t => t.RegistradaPorId == usuarioId);
    }

    /// <param name="selecionadas">Só estas trocas (linhas marcadas na grade), ignorando os demais filtros.</param>
    internal static async Task<List<LinhaDeTroca>> ListaAsync(VendasDbContext db, IEmpresaAtiva empresa, Guid? usuarioId, DateOnly? de, DateOnly? ate, string? busca,
        Guid? vendaId, CancellationToken ct, IReadOnlySet<Guid>? selecionadas = null)
    {
        var q = Visiveis(db, empresa, usuarioId);
        if (selecionadas is { Count: > 0 })
        {
            var lista = selecionadas.ToList();
            (de, ate, busca, vendaId) = (null, null, null, null);
            q = q.Where(t => lista.Contains(t.Id));
        }
        if (de is { } d) q = q.Where(t => t.Data >= d);
        if (ate is { } a) q = q.Where(t => t.Data <= a);
        if (vendaId is { } v) q = q.Where(t => t.VendaId == v);
        var trocas = await q.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = Kamus.SharedKernel.Busca.TextoParaBusca.Normalizar(busca);
            trocas = trocas.Where(t => t.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture) == busca.Trim()
                || t.VendaNumero.ToString(System.Globalization.CultureInfo.InvariantCulture) == busca.Trim()
                || Kamus.SharedKernel.Busca.TextoParaBusca.Normalizar(t.ClienteNome).Contains(termo, StringComparison.Ordinal)).ToList();
        }
        var valeIds = trocas.Where(t => t.ValeId is not null).Select(t => t.ValeId!.Value).ToList();
        var codigos = await db.Vales.AsNoTracking().Where(x => valeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Codigo, ct);
        return trocas.OrderByDescending(t => t.Data).ThenByDescending(t => t.Numero)
            .Select(t => new LinhaDeTroca(t.Id, t.Numero, t.Data, t.VendaNumero, t.ClienteNome, t.Motivo.ToString(), t.Devolvidos.Sum(x => x.Quantidade), t.ValorDevolvido,
                t.ValorLevado, t.Diferenca, t.ValeId is { } id ? codigos.GetValueOrDefault(id) : null)).ToList();
    }

    /// <summary>Sem <c>pagina</c>, a lista completa (como antes); com <c>pagina</c>, <c>{ itens, total, pagina, tamanho }</c>.</summary>
    private static async Task<IResult> Listar(DateOnly? de, DateOnly? ate, string? busca, Guid? vendaId, int? pagina, int? tamanho, VendasDbContext db,
        IEmpresaAtiva empresa, IUsuarioAtual usuario, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as vendas.") is { } negado) return negado;
        var linhas = await ListaAsync(db, empresa, usuario.Id, de, ate, busca, vendaId, ct);
        return pagina is null ? Results.Ok(linhas) : Results.Ok(linhas.PaginarSePedido(pagina, tamanho));
    }

    private static async Task<TrocaDto> DtoAsync(VendasDbContext db, Troca t, DateOnly hoje, IReadOnlyList<string>? avisos, CancellationToken ct)
    {
        var vale = t.ValeId is { } id ? await db.Vales.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, ct) : null;
        return new TrocaDto(t.Id, t.Numero, t.Data, t.VendaId, t.VendaNumero, t.ClienteNome, t.VendedorNome, t.Motivo.ToString(), t.Observacao, t.ValorDevolvido,
            t.ValorLevado, t.Diferenca, t.Troco, t.Comissao, t.RegistradaPor,
            t.Devolvidos.Select(d => new ItemDevolvidoDto(d.SkuId, d.Descricao, d.Quantidade, d.Valor, d.Destino.ToString())).ToList(),
            t.Levados.Select(l => new ItemLevadoDto(l.SkuId, l.Descricao, l.Quantidade, l.PrecoUnitario, l.Desconto, l.Total)).ToList(),
            t.Pagamentos.Select(p => new VendasEndpoints.PagamentoDto(p.FormaId, p.FormaNome, p.Tipo.ToString(), p.Valor, p.Parcelas, p.TaxaPercentual, p.ValorTaxa, p.ValeId)).ToList(),
            vale is null ? null : ValesEndpoints.Dto(vale, hoje), avisos ?? []);
    }

    private static async Task<IResult> Obter(Guid id, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as vendas.") is { } negado) return negado;
        var troca = await Visiveis(db, empresa, usuario.Id).SingleOrDefaultAsync(t => t.Id == id, ct);
        return troca is null ? Results.NotFound() : Results.Ok(await DtoAsync(db, troca, CalendarioBrasilia.Hoje(relogio), null, ct));
    }

    private static async Task<IResult> Registrar(TrocaRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, ICatalogoDeProdutos catalogo,
        IMovimentacaoDeEstoque estoque, IContasAReceber financeiro, ICrediario crediario, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Registrar, "Só Dono, Gerente e Vendedor registram trocas.") is { } negado) return negado;
        var empresaId = empresa.EmpresaId!.Value;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var venda = await db.Vendas.Include(v => v.Itens).Include(v => v.Pagamentos).SingleOrDefaultAsync(v => v.Id == r.VendaId && v.EmpresaId == empresaId, ct);
        if (venda is null) return Permissoes.Erro("Venda não encontrada nesta empresa.");
        if (!Enum.TryParse<MotivoDaTroca>(r.Motivo, ignoreCase: true, out var motivo) || !Enum.IsDefined(motivo))
            return Permissoes.Erro("Motivo inválido: Tamanho, Cor, Defeito, Desistencia ou Outro.");
        var devolvidos = new List<DevolucaoNova>();
        foreach (var d in r.Devolvidos ?? [])
        {
            if (!Enum.TryParse<DestinoDaDevolucao>(d.Destino ?? "Estoque", ignoreCase: true, out var destino) || !Enum.IsDefined(destino))
                return Permissoes.Erro("Destino inválido: Estoque ou Avariado.");
            devolvidos.Add(new DevolucaoNova(d.SkuId, d.Quantidade, destino));
        }

        var configuracao = await FormasEndpoints.ConfiguracaoAsync(db, empresaId, ct);
        var dias = hoje.DayNumber - venda.Data.DayNumber;
        if (dias > configuracao.PrazoDeTrocaDias && !(r.ForaDoPrazo && Permissoes.Gestao.Contains(empresa.Perfil)))
            return Permissoes.Erro($"A venda nº {venda.Numero} foi há {dias} dias e o prazo de troca é de {configuracao.PrazoDeTrocaDias} dias. "
                + "Dono ou Gerente podem aceitar fora do prazo.");

        var levados = r.Levados ?? [];
        var skus = (await catalogo.SkusAsync(levados.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (levados.Any(i => !skus.ContainsKey(i.SkuId))) return Permissoes.Erro("Há peças levadas que não existem no catálogo.");
        if (levados.FirstOrDefault(i => !skus[i.SkuId].Ativo) is { } inativo) return Permissoes.Erro($"{Nome(skus[inativo.SkuId])} está inativo no catálogo.");

        var (pagamentos, formas, erro) = await Pagamentos.ResolverAsync(db, financeiro, crediario, empresaId, venda.ClienteId, r.Pagamentos ?? [], hoje, ct);
        if (erro is not null) return Permissoes.Erro(erro);

        var numero = (await db.Trocas.Where(t => t.EmpresaId == empresaId).MaxAsync(t => (int?)t.Numero, ct) ?? 0) + 1;
        Troca troca;
        try
        {
            troca = Troca.Criar(new DadosDaTroca(empresaId, venda.LojaId, numero, hoje, motivo, r.Observacao, usuario.Id), venda, devolvidos,
                levados.Select(i => new ItemNovo(i.SkuId, skus[i.SkuId].ProdutoId, Nome(skus[i.SkuId]), i.Quantidade, skus[i.SkuId].PrecoVenda, i.Desconto)).ToList(),
                pagamentos, await JaDevolvidasAsync(db, venda.Id, ct), usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }

        var documento = $"Troca nº {numero}";
        var paraEstoque = troca.Devolvidos.Where(d => d.Destino == DestinoDaDevolucao.Estoque).ToList();
        Guid? devolucao = null;
        SaidaRegistrada? saida = null;
        try
        {
            if (troca.Levados.Count > 0)
                saida = await estoque.RegistrarVendaAsync(new SaidaDeEstoque(empresaId, troca.LojaId, documento,
                    troca.Levados.Select(i => new ItemDeSaida(i.SkuId, i.Quantidade)).ToList(), configuracao.PermitirEstoqueNegativo), ct);
        }
        catch (SaidaDeEstoqueRecusadaException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message);
        }

        try
        {
            if (paraEstoque.Count > 0)
                devolucao = await estoque.RegistrarDevolucaoAsync(new DevolucaoDeEstoque(empresaId, troca.LojaId, documento,
                    paraEstoque.Select(d => new ItemDevolvidoAoEstoque(d.SkuId, d.Quantidade, d.CustoUnitario)).ToList()), ct);
            troca.RegistrarEstoque(devolucao, saida?.OperacaoId, saida?.Itens.ToDictionary(i => i.SkuId, i => i.CustoUnitario) ?? []);

            if (troca.Pagamentos.Count > 0)
                await financeiro.RegistrarVendaAsync(Pagamentos.Recebiveis(empresaId, Origem, troca.Id, documento, troca.ClienteId, troca.ClienteNome, troca.Data,
                    troca.Pagamentos.Select(p => new PagamentoGravado(p.FormaId, p.FormaNome, p.Tipo, p.Valor, p.Parcelas, p.TaxaPercentual, p.ValorTaxa)), formas), ct);

            if (troca.ValorDoVale > 0)
            {
                var vale = ValeTroca.Emitir(empresaId, await ValesEndpoints.CodigoLivreAsync(db, ct), troca.ClienteId, troca.ClienteNome, troca.ValorDoVale, hoje,
                    configuracao.ValidadeDoValeDias, troca.Id);
                db.Vales.Add(vale);
                troca.VincularVale(vale.Id);
            }
            db.Trocas.Add(troca);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sem transação entre módulos: desfaz a saída das peças levadas e o financeiro (a devolução ao estoque fica — ver débitos técnicos).
            if (saida is not null) await estoque.EstornarVendaAsync(saida.OperacaoId, $"{documento} não concluída", CancellationToken.None);
            await financeiro.CancelarOrigemAsync(Origem, troca.Id.ToString(), usuario.Nome, CancellationToken.None);
            throw;
        }

        await eventos.RegistrarAsync("troca_registrada",
            $"Troca nº {troca.Numero} da venda nº {troca.VendaNumero}: devolvido R$ {Dinheiro.Formatar(troca.ValorDevolvido)}, levado R$ {Dinheiro.Formatar(troca.ValorLevado)}", ct: ct);
        var avisos = saida?.Itens.Where(i => i.SaldoApos < 0).Select(i => $"{Nome(skus[i.SkuId])} ficou com estoque {i.SaldoApos}.").ToList();
        return Results.Created($"/api/vendas/trocas/{troca.Id}", await DtoAsync(db, troca, hoje, avisos, ct));
    }

    private static string Nome(SkuDoCatalogo sku) => $"{sku.Descricao} {sku.Cor} {sku.Tamanho}";
}
