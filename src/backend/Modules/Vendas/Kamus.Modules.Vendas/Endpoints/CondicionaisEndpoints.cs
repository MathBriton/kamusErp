using Kamus.Modules.Vendas.Aplicacao;
using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Condicional (malinha): abrir (tira do estoque), consultar, termo em PDF, fechar (devolve parte e vende o resto) e conversão por vendedor.</summary>
internal static class CondicionaisEndpoints
{
    public sealed record CondicionalRequest(Guid LojaId, Guid ClienteId, string? ClienteNome, Guid? VendedorId, DateOnly? Data, DateOnly? Prazo,
        IReadOnlyList<VendasEndpoints.ItemRequest>? Itens, string? Observacao, bool Liberar);
    public sealed record DevolvidaRequest(Guid SkuId, int Quantidade);
    public sealed record FecharRequest(IReadOnlyList<DevolvidaRequest>? Devolvidos, IReadOnlyList<PagamentoPedido>? Pagamentos, decimal DescontoGeral,
        string? NotaExterna);

    public sealed record ItemDto(Guid SkuId, string Descricao, int Quantidade, decimal PrecoUnitario, int Devolvidas);
    public sealed record CondicionalDto(Guid Id, int Numero, DateOnly Data, DateOnly Prazo, Guid ClienteId, string ClienteNome, Guid? VendedorId, string? VendedorNome,
        string Situacao, int Pecas, decimal Valor, IReadOnlyList<ItemDto> Itens, Guid? VendaId, int? VendaNumero, string? Observacao, string? RegistradaPor);
    public sealed record FechamentoDto(CondicionalDto Condicional, int? VendaNumero, decimal? VendaTotal, IReadOnlyList<string> Avisos);
    public sealed record LinhaDeCondicional(Guid Id, int Numero, DateOnly Data, DateOnly Prazo, Guid ClienteId, string ClienteNome, string? VendedorNome, int Pecas,
        decimal Valor, string Situacao, int? VendaNumero);
    public sealed record ListaDeCondicionais(IReadOnlyList<LinhaDeCondicional> Itens, int Abertas, int Atrasadas, int PecasNaRua, decimal ValorNaRua);
    public sealed record ConversaoDto(Guid? VendedorId, string Vendedor, int Condicionais, int PecasLevadas, int PecasVendidas, decimal ValorLevado,
        decimal ValorVendido, decimal Conversao);

    public static readonly string[] Situacoes = ["abertas", "atrasadas", "fechadas", "todas"];
    private static readonly string[] Perfis = [.. Permissoes.Registrar, .. Permissoes.Consultar];

    public static IEndpointRouteBuilder MapCondicionais(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vendas/condicionais").RequireAuthorization().WithTags("Vendas");
        g.MapGet("/", Listar).WithSummary("Condicionais (situacao: abertas, atrasadas, fechadas, todas; clienteId; busca) com peças e valor \"na rua\"");
        g.MapGet("/conversao", Conversao).WithSummary("Conversão por vendedor: quanto das condicionais fechadas no período virou venda");
        g.MapGet("/{id:guid}", Obter).WithSummary("Condicional com as peças");
        g.MapGet("/{id:guid}/termo.pdf", Termo).WithSummary("Termo de responsabilidade em PDF (peças, valores e prazo)");
        g.MapPost("/", Abrir).WithSummary("Abre a condicional: as peças saem do estoque disponível. Limites e atraso do cliente só Dono/Gerente liberam")
            .Produces<CondicionalDto>(StatusCodes.Status201Created);
        g.MapPost("/{id:guid}/fechar", Fechar).WithSummary("Fecha: as peças devolvidas voltam ao estoque e as que o cliente ficou viram venda");
        return app;
    }

    internal static CondicionalDto Dto(Condicional c, DateOnly hoje) => new(c.Id, c.Numero, c.Data, c.Prazo, c.ClienteId, c.ClienteNome, c.VendedorId, c.VendedorNome,
        c.Situacao(hoje).ToString(), c.Pecas, c.Valor, c.Itens.Select(i => new ItemDto(i.SkuId, i.Descricao, i.Quantidade, i.PrecoUnitario, i.Devolvidas)).ToList(),
        c.VendaId, c.VendaNumero, c.Observacao, c.RegistradaPor);

    internal static async Task<ListaDeCondicionais> ListaAsync(VendasDbContext db, IEmpresaAtiva empresa, string situacao, Guid? clienteId, string? busca, DateOnly hoje,
        CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        var q = db.Condicionais.AsNoTracking().Include(c => c.Itens).Where(c => ids.Contains(c.EmpresaId));
        if (clienteId is { } cliente) q = q.Where(c => c.ClienteId == cliente);
        if (situacao != "todas") q = situacao == "fechadas" ? q.Where(c => c.Fechada) : q.Where(c => !c.Fechada);
        var todas = await q.ToListAsync(ct);
        if (situacao == "atrasadas") todas = todas.Where(c => c.Situacao(hoje) == SituacaoDaCondicional.Atrasada).ToList();
        var termo = TextoParaBusca.Normalizar(busca);
        if (!string.IsNullOrEmpty(termo))
            todas = todas.Where(c => c.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture) == busca!.Trim()
                || TextoParaBusca.Normalizar(c.ClienteNome).Contains(termo, StringComparison.Ordinal)).ToList();
        var abertas = todas.Where(c => !c.Fechada).ToList();
        return new ListaDeCondicionais(
            todas.OrderBy(c => c.Fechada).ThenBy(c => c.Fechada ? DateOnly.MaxValue : c.Prazo).ThenByDescending(c => c.Numero)
                .Select(c => new LinhaDeCondicional(c.Id, c.Numero, c.Data, c.Prazo, c.ClienteId, c.ClienteNome, c.VendedorNome, c.Pecas, c.Valor,
                    c.Situacao(hoje).ToString(), c.VendaNumero)).ToList(),
            abertas.Count, abertas.Count(c => c.Situacao(hoje) == SituacaoDaCondicional.Atrasada), abertas.Sum(c => c.Pecas), abertas.Sum(c => c.Valor));
    }

    private static async Task<IResult> Listar(string? situacao, Guid? clienteId, string? busca, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio,
        CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as condicionais.") is { } negado) return negado;
        var s = situacao?.ToLowerInvariant() ?? "abertas";
        if (!Situacoes.Contains(s)) return Permissoes.Erro("Situação inválida: abertas, atrasadas, fechadas ou todas.");
        return Results.Ok(await ListaAsync(db, empresa, s, clienteId, busca, CalendarioBrasilia.Hoje(relogio), ct));
    }

    private static async Task<Condicional?> CarregarAsync(VendasDbContext db, IEmpresaAtiva empresa, Guid id, CancellationToken ct)
    {
        var ids = empresa.EmpresaIds;
        return await db.Condicionais.Include(c => c.Itens).SingleOrDefaultAsync(c => c.Id == id && ids.Contains(c.EmpresaId), ct);
    }

    private static async Task<IResult> Obter(Guid id, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as condicionais.") is { } negado) return negado;
        var c = await CarregarAsync(db, empresa, id, ct);
        return c is null ? Results.NotFound() : Results.Ok(Dto(c, CalendarioBrasilia.Hoje(relogio)));
    }

    private static async Task<IResult> Termo(Guid id, VendasDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Perfis, "Seu perfil não acessa as condicionais.") is { } negado) return negado;
        var c = await CarregarAsync(db, empresa, id, ct);
        if (c is null) return Results.NotFound();
        return Results.File(TermoDeCondicional.Gerar(c, empresa.Nome ?? "", empresa.Cnpj), "application/pdf", $"condicional-{c.Numero}.pdf");
    }

    private static async Task<IResult> Abrir(CondicionalRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, ICatalogoDeProdutos catalogo,
        IDiretorioDeVendedores vendedores, IMovimentacaoDeEstoque estoque, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Registrar, "Só Dono, Gerente e Vendedor abrem condicionais.") is { } negado) return negado;
        if (r.ClienteId == Guid.Empty || string.IsNullOrWhiteSpace(r.ClienteNome)) return Permissoes.Erro("Condicional precisa de cliente cadastrado: escolha o cliente.");
        var empresaId = empresa.EmpresaId!.Value;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var data = r.Data ?? hoje;
        if (data > hoje) return Permissoes.Erro("A data da condicional não pode ser futura.");
        var configuracao = await FormasEndpoints.ConfiguracaoAsync(db, empresaId, ct);
        var prazo = r.Prazo ?? data.AddDays(configuracao.PrazoCondicionalDias);
        var gestao = Permissoes.Gestao.Contains(empresa.Perfil);
        var liberar = r.Liberar && gestao;

        VendedorResumo? vendedor = null;
        if (r.VendedorId is { } vendedorId)
        {
            vendedor = await vendedores.ObterAsync(vendedorId, ct);
            if (vendedor is null || !vendedor.Ativo) return Permissoes.Erro("Vendedor não encontrado ou inativo.");
        }

        var itens = r.Itens ?? [];
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (itens.Any(i => !skus.ContainsKey(i.SkuId))) return Permissoes.Erro("Há peças que não existem no catálogo.");
        if (itens.FirstOrDefault(i => !skus[i.SkuId].Ativo) is { } inativo) return Permissoes.Erro($"{Nome(skus[inativo.SkuId])} está inativo no catálogo.");

        var abertas = await db.Condicionais.AsNoTracking().Include(c => c.Itens).Where(c => c.ClienteId == r.ClienteId && !c.Fechada).ToListAsync(ct);
        var atrasada = abertas.Where(c => c.Situacao(hoje) == SituacaoDaCondicional.Atrasada).OrderBy(c => c.Prazo).FirstOrDefault();
        if (atrasada is not null && !liberar)
            return Permissoes.Erro($"O cliente tem a condicional nº {atrasada.Numero} atrasada (prazo {atrasada.Prazo:dd/MM/yyyy}). Só Dono ou Gerente liberam.");

        var numero = (await db.Condicionais.Where(c => c.EmpresaId == empresaId).MaxAsync(c => (int?)c.Numero, ct) ?? 0) + 1;
        Condicional condicional;
        try
        {
            condicional = Condicional.Abrir(
                new DadosDaCondicional(empresaId, r.LojaId, numero, data, prazo, r.ClienteId, r.ClienteNome, vendedor?.Id, vendedor?.Nome, r.Observacao, usuario.Id),
                itens.Select(i => new ItemNovo(i.SkuId, skus[i.SkuId].ProdutoId, Nome(skus[i.SkuId]), i.Quantidade, skus[i.SkuId].PrecoVenda, 0)).ToList(),
                usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        if (Condicional.ConferirLimites(configuracao, abertas.Sum(c => c.Pecas), abertas.Sum(c => c.Valor), condicional.Pecas, condicional.Valor) is { } limite && !liberar)
            return Permissoes.Erro($"{limite} Só Dono ou Gerente liberam.");

        SaidaRegistrada saida;
        try
        {
            saida = await estoque.RegistrarSaidaParaCondicionalAsync(new SaidaDeEstoque(empresaId, r.LojaId, $"Condicional nº {numero}",
                condicional.Itens.Select(i => new ItemDeSaida(i.SkuId, i.Quantidade)).ToList(), PermitirNegativo: false), ct);
        }
        catch (SaidaDeEstoqueRecusadaException ex)
        {
            return Permissoes.Erro(ex.Message);
        }
        condicional.RegistrarSaida(saida.OperacaoId, saida.Itens.ToDictionary(i => i.SkuId, i => i.CustoUnitario));
        try
        {
            db.Condicionais.Add(condicional);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await estoque.RegistrarRetornoDeCondicionalAsync(new DevolucaoDeEstoque(empresaId, r.LojaId, $"Condicional nº {numero} não concluída",
                condicional.Itens.Select(i => new ItemDevolvidoAoEstoque(i.SkuId, i.Quantidade, i.CustoUnitario)).ToList()), CancellationToken.None);
            throw;
        }
        await eventos.RegistrarAsync("condicional_aberta",
            $"Condicional nº {numero} para {condicional.ClienteNome}: {condicional.Pecas} peças (R$ {Dinheiro.Formatar(condicional.Valor)}), prazo {prazo:dd/MM/yyyy}"
            + (liberar && (atrasada is not null) ? " — liberada com condicional atrasada" : ""), ct: ct);
        return Results.Created($"/api/vendas/condicionais/{condicional.Id}", Dto(condicional, hoje));
    }

    private static async Task<IResult> Fechar(Guid id, FecharRequest r, VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, IMovimentacaoDeEstoque estoque,
        RegistroDeVendas registro, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Registrar, "Só Dono, Gerente e Vendedor fecham condicionais.") is { } negado) return negado;
        var condicional = await CarregarAsync(db, empresa, id, ct);
        if (condicional is null) return Results.NotFound();
        if (condicional.Fechada) return Permissoes.Problema(StatusCodes.Status409Conflict, $"A condicional nº {condicional.Numero} já foi fechada.");
        var devolvidas = (r.Devolvidos ?? []).GroupBy(d => d.SkuId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantidade));
        IReadOnlyList<PecaQueFicou> ficou;
        try
        {
            ficou = condicional.Conferir(devolvidas);
        }
        catch (ArgumentException ex)
        {
            return Permissoes.Erro(Permissoes.Mensagem(ex));
        }
        var pagamentos = r.Pagamentos ?? [];
        if (ficou.Count > 0 && pagamentos.Count == 0) return Permissoes.Erro("O cliente ficou com peças: informe a forma de pagamento da venda.");

        var documento = $"Condicional nº {condicional.Numero}";
        var todas = condicional.Itens.Select(i => new ItemDevolvidoAoEstoque(i.SkuId, i.Quantidade, i.CustoUnitario)).ToList();
        await estoque.RegistrarRetornoDeCondicionalAsync(new DevolucaoDeEstoque(condicional.EmpresaId, condicional.LojaId, documento, todas), ct);

        ResultadoDaVenda? venda = null;
        if (ficou.Count > 0)
        {
            venda = await registro.RegistrarAsync(new VendasEndpoints.VendaRequest(condicional.LojaId, null, condicional.ClienteId, condicional.ClienteNome,
                condicional.VendedorId, ficou.Select(p => new VendasEndpoints.ItemRequest(p.SkuId, p.Quantidade, 0)).ToList(), r.DescontoGeral, pagamentos,
                r.NotaExterna, documento), ct);
            if (venda.Erro is not null)
            {
                // A venda foi recusada: as peças voltam para a condicional.
                await estoque.RegistrarSaidaParaCondicionalAsync(new SaidaDeEstoque(condicional.EmpresaId, condicional.LojaId, $"{documento} (venda recusada)",
                    todas.Select(i => new ItemDeSaida(i.SkuId, i.Quantidade)).ToList(), false), CancellationToken.None);
                return venda.Erro;
            }
        }

        condicional.Fechar(devolvidas, venda?.Venda?.Id, venda?.Venda?.Numero, usuario.Nome, relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await eventos.RegistrarAsync("condicional_fechada",
            $"{documento} de {condicional.ClienteNome} fechada: {condicional.Pecas - condicional.PecasVendidas} peças devolvidas"
            + (venda?.Venda is { } v ? $", venda nº {v.Numero} (R$ {Dinheiro.Formatar(v.Total)})" : ""), ct: ct);
        return Results.Ok(new FechamentoDto(Dto(condicional, CalendarioBrasilia.Hoje(relogio)), venda?.Venda?.Numero, venda?.Venda?.Total, venda?.Avisos ?? []));
    }

    private static async Task<IResult> Conversao(DateOnly? de, DateOnly? ate, VendasDbContext db, IEmpresaAtiva empresa, TimeProvider relogio, CancellationToken ct)
    {
        if (Permissoes.Exigir(empresa, Permissoes.Consultar, "Só Dono, Gerente e Financeiro veem a conversão.") is { } negado) return negado;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var (inicio, fim) = (de ?? new DateOnly(hoje.Year, hoje.Month, 1), ate ?? hoje);
        var ids = empresa.EmpresaIds;
        var fechadas = await db.Condicionais.AsNoTracking().Include(c => c.Itens)
            .Where(c => ids.Contains(c.EmpresaId) && c.Fechada && c.Data >= inicio && c.Data <= fim).ToListAsync(ct);
        return Results.Ok(fechadas.GroupBy(c => c.VendedorId).Select(g =>
        {
            var levado = g.Sum(c => c.Valor);
            var vendido = g.Sum(c => c.ValorVendido);
            return new ConversaoDto(g.Key, g.Key is null ? "Sem vendedor" : g.First().VendedorNome ?? "?", g.Count(), g.Sum(c => c.Pecas), g.Sum(c => c.PecasVendidas),
                levado, vendido, levado == 0 ? 0 : Math.Round(vendido / levado * 100, 2, MidpointRounding.AwayFromZero));
        }).OrderBy(c => c.VendedorId is null).ThenBy(c => c.Vendedor, StringComparer.CurrentCulture).ToList());
    }

    private static string Nome(SkuDoCatalogo sku) => $"{sku.Descricao} {sku.Cor} {sku.Tamanho}";
}
