using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Endpoints;

/// <summary>Produtos com grade: cada cor × tamanho é um SKU com código de barras próprio.</summary>
internal static class ProdutosEndpoints
{
    public sealed record ProdutoRequest(
        string? Referencia, string? Descricao, string? Marca, string? Categoria, string? Colecao, string? ReferenciaDoFornecedor,
        Guid? FornecedorId, decimal PrecoVenda, decimal? PrecoCusto, Guid GradeId, IReadOnlyList<Guid>? CorIds);
    public sealed record SkuRequest(string? CodigoDeBarras, decimal? PrecoVenda, bool Ativo);
    public sealed record SkuDto(Guid Id, Guid CorId, string Tamanho, int OrdemTamanho, string CodigoDeBarras, decimal? PrecoVenda, decimal Preco, bool Ativo);
    public sealed record ProdutoDto(
        Guid Id, string Referencia, string Descricao, string? Marca, string? Categoria, string? Colecao, string? ReferenciaDoFornecedor,
        Guid? FornecedorId, decimal PrecoVenda, decimal? PrecoCusto, decimal? Markup, GradesECoresEndpoints.GradeDto Grade,
        IReadOnlyList<GradesECoresEndpoints.CorDto> Cores, IReadOnlyList<SkuDto> Skus, bool Ativo);
    public sealed record ResumoDto(Guid Id, string Referencia, string Descricao, string? Marca, string? Categoria, string? Colecao,
        decimal PrecoVenda, decimal? Markup, int Skus, IReadOnlyList<string> Cores, bool Ativo);
    public sealed record PaginaDto(IReadOnlyList<ResumoDto> Itens, int Total, int Pagina, int Tamanho);
    public sealed record PorCodigoDto(ProdutoDto Produto, SkuDto Sku, string Cor);
    public sealed record SugestoesDto(IReadOnlyList<string> Marcas, IReadOnlyList<string> Categorias, IReadOnlyList<string> Colecoes);

    public static IEndpointRouteBuilder MapProdutos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/produtos").RequireAuthorization().WithTags("Produtos");
        g.MapGet("", Listar).WithSummary("Produtos (busca por descrição, referência, marca ou código de barras)").Produces<PaginaDto>();
        g.MapGet("/sugestoes", Sugestoes).WithSummary("Marcas, categorias e coleções já usadas (para completar o formulário)").Produces<SugestoesDto>();
        g.MapGet("/por-codigo/{codigo}", PorCodigo).WithSummary("SKU pelo código de barras (leitor)").Produces<PorCodigoDto>();
        g.MapGet("/{id:guid}", Obter).WithSummary("Produto com a grade, cores e SKUs").Produces<ProdutoDto>();
        g.MapPost("", Criar).WithSummary("Cadastrar produto: gera os SKUs de grade × cores com código interno")
            .Produces<ProdutoDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        g.MapPut("/{id:guid}", Editar).WithSummary("Alterar dados e cores (a referência e a grade não mudam)").Produces<ProdutoDto>();
        g.MapPut("/{id:guid}/skus/{skuId:guid}", AlterarSku).WithSummary("Código de barras, preço próprio e ativo do SKU").Produces<SkuDto>();
        g.MapPost("/{id:guid}/inativar", (Guid id, IEmpresaAtiva e, CadastrosDbContext db, CancellationToken ct) => Mudar(id, e, db, p => p.Inativar(), ct))
            .WithSummary("Inativar produto").Produces(StatusCodes.Status204NoContent);
        g.MapPost("/{id:guid}/reativar", (Guid id, IEmpresaAtiva e, CadastrosDbContext db, CancellationToken ct) => Mudar(id, e, db, p => p.Reativar(), ct))
            .WithSummary("Reativar produto").Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static IResult Problema(int status, string detalhe) => GradesECoresEndpoints.Problema(status, detalhe);
    private static readonly IResult NaoPode = Permissoes.Negado("Só Dono, Gerente e Estoquista alteram o catálogo.");

    private static IQueryable<Produto> ComSkus(CadastrosDbContext db) => db.Produtos.Include(p => p.Skus);

    internal static async Task<ProdutoDto> ParaDtoAsync(Produto p, CadastrosDbContext db, CancellationToken ct)
    {
        var grade = await db.Grades.AsNoTracking().SingleAsync(g => g.Id == p.GradeId, ct);
        var corIds = p.Skus.Select(s => s.CorId).Distinct().ToList();
        var cores = await db.Cores.AsNoTracking().Where(c => corIds.Contains(c.Id)).ToListAsync(ct);
        return new ProdutoDto(p.Id, p.Referencia, p.Descricao, p.Marca, p.Categoria, p.Colecao, p.ReferenciaDoFornecedor, p.FornecedorId,
            p.PrecoVenda, p.PrecoCusto, p.Markup, new GradesECoresEndpoints.GradeDto(grade.Id, grade.Nome, grade.Tamanhos, true),
            cores.OrderBy(c => corIds.IndexOf(c.Id)).Select(c => new GradesECoresEndpoints.CorDto(c.Id, c.Nome, c.Hex)).ToList(),
            p.Skus.OrderBy(s => corIds.IndexOf(s.CorId)).ThenBy(s => s.OrdemTamanho).Select(s => SkuParaDto(p, s)).ToList(), p.Ativo);
    }

    private static SkuDto SkuParaDto(Produto p, Sku s) => new(s.Id, s.CorId, s.Tamanho, s.OrdemTamanho, s.CodigoDeBarras, s.PrecoVenda, p.PrecoDe(s), s.Ativo);

    /// <summary>Próximo código interno (EAN-13 prefixo 2): continua a sequência do cliente.</summary>
    private static async Task<Func<string>> GeradorDeCodigosAsync(CadastrosDbContext db, CancellationToken ct)
    {
        var internos = await db.Skus.Where(s => s.CodigoDeBarras.Length == 13).Select(s => s.CodigoDeBarras).ToListAsync(ct);
        var ultimo = internos.Select(CodigoDeBarras.SequencialInterno).Max() ?? 0;
        return () => CodigoDeBarras.Interno(++ultimo);
    }

    internal static bool Corresponde(Produto p, string termo) =>
        TextoParaBusca.Contem(p.Descricao, termo) || TextoParaBusca.Contem(p.Referencia, termo) || TextoParaBusca.Contem(p.Marca, termo)
        || TextoParaBusca.Contem(p.ReferenciaDoFornecedor, termo) || p.Skus.Any(s => s.CodigoDeBarras == termo.Trim());

    internal static async Task<List<Produto>> FiltrarAsync(CadastrosDbContext db, string? busca, string? categoria, string? colecao, string? situacao, CancellationToken ct)
    {
        var consulta = ComSkus(db).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoria)) consulta = consulta.Where(p => p.Categoria == categoria);
        if (!string.IsNullOrWhiteSpace(colecao)) consulta = consulta.Where(p => p.Colecao == colecao);
        consulta = situacao?.ToLowerInvariant() switch
        {
            "inativos" => consulta.Where(p => !p.Ativo),
            "todos" => consulta,
            _ => consulta.Where(p => p.Ativo),
        };
        IEnumerable<Produto> produtos = await consulta.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(busca)) produtos = produtos.Where(p => Corresponde(p, busca));
        return produtos.OrderBy(p => p.Descricao, StringComparer.CurrentCulture).ToList();
    }

    private static async Task<IResult> Listar(CadastrosDbContext db, CancellationToken ct,
        string? busca = null, string? categoria = null, string? colecao = null, string? situacao = null, int pagina = 1, int tamanho = 50)
    {
        var todos = await FiltrarAsync(db, busca, categoria, colecao, situacao, ct);
        var cores = await db.Cores.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 200);
        var itens = todos.Skip((pagina - 1) * tamanho).Take(tamanho).Select(p => new ResumoDto(p.Id, p.Referencia, p.Descricao, p.Marca, p.Categoria, p.Colecao,
            p.PrecoVenda, p.Markup, p.Skus.Count(s => s.Ativo),
            p.Skus.Where(s => s.Ativo).Select(s => s.CorId).Distinct().Select(c => cores.GetValueOrDefault(c, "?")).ToList(), p.Ativo)).ToList();
        return Results.Ok(new PaginaDto(itens, todos.Count, pagina, tamanho));
    }

    private static async Task<IResult> Sugestoes(CadastrosDbContext db, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking().Select(p => new { p.Marca, p.Categoria, p.Colecao }).ToListAsync(ct);
        static List<string> Distintos(IEnumerable<string?> valores) =>
            valores.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCulture).ToList();
        return Results.Ok(new SugestoesDto(Distintos(produtos.Select(p => p.Marca)), Distintos(produtos.Select(p => p.Categoria)), Distintos(produtos.Select(p => p.Colecao))));
    }

    private static async Task<IResult> PorCodigo(string codigo, CadastrosDbContext db, CancellationToken ct)
    {
        var normalizado = CodigoDeBarras.Normalizar(codigo) ?? codigo;
        var sku = await db.Skus.AsNoTracking().SingleOrDefaultAsync(s => s.CodigoDeBarras == normalizado, ct);
        if (sku is null) return Problema(StatusCodes.Status404NotFound, "Nenhum produto com este código de barras.");
        var produto = await ComSkus(db).AsNoTracking().SingleAsync(p => p.Id == sku.ProdutoId, ct);
        var dto = await ParaDtoAsync(produto, db, ct);
        return Results.Ok(new PorCodigoDto(dto, dto.Skus.Single(s => s.Id == sku.Id), dto.Cores.Single(c => c.Id == sku.CorId).Nome));
    }

    private static async Task<IResult> Obter(Guid id, CadastrosDbContext db, CancellationToken ct)
    {
        var produto = await ComSkus(db).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return produto is null ? Problema(StatusCodes.Status404NotFound, "Produto não encontrado.") : Results.Ok(await ParaDtoAsync(produto, db, ct));
    }

    private static DadosDoProduto Dados(ProdutoRequest r) => new()
    {
        Referencia = r.Referencia,
        Descricao = r.Descricao,
        Marca = r.Marca,
        Categoria = r.Categoria,
        Colecao = r.Colecao,
        ReferenciaDoFornecedor = r.ReferenciaDoFornecedor,
        FornecedorId = r.FornecedorId,
        PrecoVenda = r.PrecoVenda,
        PrecoCusto = r.PrecoCusto,
    };

    private static async Task<string?> ValidarAsync(ProdutoRequest r, DadosDoProduto dados, CadastrosDbContext db, CancellationToken ct)
    {
        var erros = Produto.Validar(dados).ToList();
        var corIds = r.CorIds ?? [];
        if (corIds.Count == 0) erros.Add("Escolha pelo menos uma cor.");
        else if (await db.Cores.CountAsync(c => corIds.Contains(c.Id), ct) != corIds.Distinct().Count()) erros.Add("Cor não encontrada.");
        if (r.FornecedorId is { } f && !await db.Pessoas.AnyAsync(p => p.Id == f && p.EhFornecedor, ct)) erros.Add("Fornecedor não encontrado.");
        return erros.Count == 0 ? null : string.Join(" ", erros);
    }

    private static async Task<IResult> Criar(ProdutoRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        if (!Permissoes.Catalogo.Contains(empresa.Perfil)) return NaoPode;
        var dados = Dados(r);
        if (await ValidarAsync(r, dados, db, ct) is { } erro) return Problema(StatusCodes.Status400BadRequest, erro);
        var grade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(g => g.Id == r.GradeId, ct);
        if (grade is null) return Problema(StatusCodes.Status400BadRequest, "Escolha uma grade de tamanhos.");
        var referencia = r.Referencia!.Trim().ToUpperInvariant();
        if (await db.Produtos.AnyAsync(p => p.Referencia == referencia, ct))
            return Problema(StatusCodes.Status409Conflict, $"Já existe um produto com a referência {referencia}.");

        var produto = Produto.Criar(dados, grade.Id, grade.Tamanhos, r.CorIds!.Distinct().ToList(), await GeradorDeCodigosAsync(db, ct), relogio.GetUtcNow());
        db.Produtos.Add(produto);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/produtos/{produto.Id}", await ParaDtoAsync(produto, db, ct));
    }

    private static async Task<IResult> Editar(Guid id, ProdutoRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!Permissoes.Catalogo.Contains(empresa.Perfil)) return NaoPode;
        var produto = await ComSkus(db).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null) return Problema(StatusCodes.Status404NotFound, "Produto não encontrado.");
        var dados = Dados(r) with { Referencia = produto.Referencia };
        if (await ValidarAsync(r, dados, db, ct) is { } erro) return Problema(StatusCodes.Status400BadRequest, erro);

        var grade = await db.Grades.AsNoTracking().SingleAsync(g => g.Id == produto.GradeId, ct);
        produto.Atualizar(dados);
        var antes = produto.Skus.Select(s => s.Id).ToHashSet();
        produto.DefinirCores(r.CorIds!.Distinct().ToList(), grade.Tamanhos, await GeradorDeCodigosAsync(db, ct));
        // SKUs novos entram como inclusão (a coleção é lida com rastreamento; os novos precisam ser adicionados).
        foreach (var novo in produto.Skus.Where(s => !antes.Contains(s.Id))) db.Skus.Add(novo);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ParaDtoAsync(produto, db, ct));
    }

    private static async Task<IResult> AlterarSku(Guid id, Guid skuId, SkuRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        if (!Permissoes.Catalogo.Contains(empresa.Perfil)) return NaoPode;
        var produto = await ComSkus(db).SingleOrDefaultAsync(p => p.Id == id, ct);
        var sku = produto?.Skus.SingleOrDefault(s => s.Id == skuId);
        if (produto is null || sku is null) return Problema(StatusCodes.Status404NotFound, "SKU não encontrado.");
        if (!CodigoDeBarras.Valido(r.CodigoDeBarras)) return Problema(StatusCodes.Status400BadRequest, "Código de barras inválido (confira o dígito verificador).");
        if (r.PrecoVenda < 0) return Problema(StatusCodes.Status400BadRequest, "O preço não pode ser negativo.");
        var codigo = CodigoDeBarras.Normalizar(r.CodigoDeBarras)!;
        var dono = await db.Skus.AsNoTracking().Where(s => s.CodigoDeBarras == codigo && s.Id != skuId).Select(s => s.ProdutoId).FirstOrDefaultAsync(ct);
        if (dono != Guid.Empty)
        {
            var outro = await db.Produtos.AsNoTracking().Where(p => p.Id == dono).Select(p => p.Referencia).SingleAsync(ct);
            return Problema(StatusCodes.Status409Conflict, $"Este código de barras já está no produto {outro}.");
        }

        produto.AlterarSku(skuId, codigo, r.PrecoVenda, r.Ativo);
        await db.SaveChangesAsync(ct);
        return Results.Ok(SkuParaDto(produto, sku));
    }

    private static async Task<IResult> Mudar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, Action<Produto> acao, CancellationToken ct)
    {
        if (!Permissoes.Catalogo.Contains(empresa.Perfil)) return NaoPode;
        var produto = await db.Produtos.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null) return Problema(StatusCodes.Status404NotFound, "Produto não encontrado.");
        acao(produto);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
