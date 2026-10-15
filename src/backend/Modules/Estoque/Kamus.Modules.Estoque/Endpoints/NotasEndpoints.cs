using System.Text;
using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.NotasFiscais;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>
/// Entrada por XML da NF-e: importar (confere destinatário, autorização e duplicidade; vincula itens pelo de-para ou EAN),
/// conferir item a item (SKU, grade, fator ou ignorar), lançar (entrada de estoque + fornecedor + de-para memorizado) ou descartar.
/// </summary>
internal static class NotasEndpoints
{
    public sealed record DistribuicaoRequest(Guid SkuId, int Quantidade);
    public sealed record ItemRequest(Guid? ProdutoId, int? Fator, bool? Ignorado, IReadOnlyList<DistribuicaoRequest>? Distribuicao);
    public sealed record LancarRequest(Guid LojaId);

    public sealed record DistribuicaoDto(Guid SkuId, string Cor, string Tamanho, int Quantidade);
    public sealed record ItemDto(int Numero, string Codigo, string? Ean, string Descricao, string? Ncm, string Unidade, decimal Quantidade, decimal ValorTotal,
        decimal CustoTotal, decimal CustoPorPeca, int Fator, decimal PecasEsperadas, string Situacao, string? OrigemDoVinculo, Guid? ProdutoId, string? Produto,
        IReadOnlyList<DistribuicaoDto> Distribuicao);
    public sealed record DuplicataDto(string Numero, DateOnly Vencimento, decimal Valor);
    public sealed record NotaDto(Guid Id, string Chave, string Numero, string Serie, DateTimeOffset EmitidaEm, string FornecedorDocumento, string FornecedorNome,
        bool FornecedorCadastrado, decimal ValorTotal, bool Autorizada, string Status, bool ProntaParaLancar, DateTimeOffset ImportadaEm, string? ImportadaPor,
        DateTimeOffset? LancadaEm, string? Loja, Guid? OperacaoId, IReadOnlyList<ItemDto> Itens, IReadOnlyList<DuplicataDto> Duplicatas);
    public sealed record ResumoDto(Guid Id, string Numero, string Serie, DateTimeOffset EmitidaEm, string FornecedorNome, decimal ValorTotal, string Status,
        int Itens, int Pendentes, DateTimeOffset ImportadaEm);

    public static IEndpointRouteBuilder MapNotas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque/notas").RequireAuthorization().WithTags("Estoque");
        g.MapGet("/", Listar).WithSummary("Notas importadas na empresa ativa (mais recentes primeiro)");
        g.MapPost("/", Importar).DisableAntiforgery()
            .WithSummary("Importa o XML da NF-e (campo 'arquivo'); itens com de-para ou EAN conhecido já vêm vinculados").Produces<NotaDto>(StatusCodes.Status201Created);
        g.MapGet("/{id:guid}", Obter).WithSummary("Nota com os itens em conferência");
        g.MapPut("/{id:guid}/itens/{numero:int}", ConferirItem).WithSummary("Vincula o item a SKU(s) do produto (grade e fator) ou marca como ignorado");
        g.MapPost("/{id:guid}/lancar", Lancar).WithSummary("Lança a entrada na loja, cadastra o fornecedor (se preciso) e memoriza o de-para");
        g.MapPost("/{id:guid}/descartar", Descartar).WithSummary("Descarta a nota sem mexer no estoque");
        return app;
    }

    private static IResult Erro(string detalhe) => Permissoes.Problema(StatusCodes.Status400BadRequest, detalhe);

    private static IResult? Barrar(IEmpresaAtiva empresa) =>
        !Permissoes.PodeMovimentar(empresa.Perfil) ? Permissoes.Negado("Só Dono, Gerente e Estoquista lançam notas de entrada.")
        : empresa.EmpresaId is null ? Erro("Escolha uma empresa (não a visão de todas) para trabalhar com notas de entrada.")
        : null;

    private static async Task<NotaDeEntrada?> CarregarAsync(EstoqueDbContext db, IEmpresaAtiva empresa, Guid id, CancellationToken ct)
    {
        var nota = await db.Notas.Include(n => n.Itens).ThenInclude(i => i.Distribuicao).Include(n => n.Duplicatas)
            .SingleOrDefaultAsync(n => n.Id == id && n.EmpresaId == empresa.EmpresaId, ct);
        nota?.Reconectar();
        return nota;
    }

    private static async Task<NotaDto> ParaDtoAsync(NotaDeEntrada n, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IDiretorioDeFornecedores fornecedores,
        CancellationToken ct)
    {
        var skuIds = n.Itens.SelectMany(i => i.Distribuicao.Select(d => d.SkuId)).Distinct().ToList();
        var skus = (await catalogo.SkusAsync(skuIds, ct)).ToDictionary(s => s.Id);
        var produtos = new Dictionary<Guid, string>();
        foreach (var produtoId in n.Itens.Where(i => i.ProdutoId is not null).Select(i => i.ProdutoId!.Value).Distinct())
            if (await catalogo.ProdutoAsync(produtoId, ct) is { } p) produtos[produtoId] = $"{p.Referencia} — {p.Descricao}";
        var cadastrado = n.FornecedorId is not null || await fornecedores.PorDocumentoAsync(n.FornecedorDocumento, ct) is not null;
        var loja = n.LojaId is null ? null : await db.Lojas.AsNoTracking().Where(l => l.Id == n.LojaId).Select(l => l.Nome).SingleOrDefaultAsync(ct);

        return new NotaDto(n.Id, n.Chave, n.Numero, n.Serie, n.EmitidaEm, n.FornecedorDocumento, n.FornecedorNome, cadastrado, n.ValorTotal, n.Autorizada,
            n.Status.ToString(), n.ProntaParaLancar, n.ImportadaEm, n.ImportadaPor, n.LancadaEm, loja, n.OperacaoId,
            n.Itens.OrderBy(i => i.Numero).Select(i => ItemParaDto(i, skus, produtos)).ToList(),
            n.Duplicatas.OrderBy(d => d.Vencimento).Select(d => new DuplicataDto(d.Numero, d.Vencimento, d.Valor)).ToList());
    }

    private static ItemDto ItemParaDto(ItemDaNota i, Dictionary<Guid, SkuDoCatalogo> skus, Dictionary<Guid, string> produtos) =>
        new(i.Numero, i.Codigo, i.Ean, i.Descricao, i.Ncm, i.Unidade, i.Quantidade, i.ValorTotal, i.CustoTotal, i.CustoPorPeca, i.Fator, i.PecasEsperadas,
            i.Situacao.ToString(), i.OrigemDoVinculo, i.ProdutoId, i.ProdutoId is { } p ? produtos.GetValueOrDefault(p) : null,
            i.Distribuicao.Select(d => skus.TryGetValue(d.SkuId, out var s)
                ? new DistribuicaoDto(d.SkuId, s.Cor, s.Tamanho, d.Quantidade) : new DistribuicaoDto(d.SkuId, "?", "?", d.Quantidade)).ToList());

    private static async Task<IResult> Listar(EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var notas = await db.Notas.AsNoTracking().Include(n => n.Itens).ThenInclude(i => i.Distribuicao)
            .Where(n => n.EmpresaId == empresa.EmpresaId && n.Status != StatusDaNota.Descartada)
            .OrderByDescending(n => n.ImportadaEm).Take(200).ToListAsync(ct);
        return Results.Ok(notas.Select(n => new ResumoDto(n.Id, n.Numero, n.Serie, n.EmitidaEm, n.FornecedorNome, n.ValorTotal, n.Status.ToString(),
            n.Itens.Count, n.Itens.Count(i => i.Situacao == SituacaoDoItem.Pendente), n.ImportadaEm)).ToList());
    }

    private static async Task<IResult> Importar(IFormFile? arquivo, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IDiretorioDeFornecedores fornecedores,
        IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        if (arquivo is null || arquivo.Length == 0) return Erro("Escolha o arquivo XML da NF-e.");
        if (!arquivo.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return Erro("Envie o arquivo .xml da NF-e (não o PDF/DANFE).");
        if (arquivo.Length > LeitorDeNfe.TamanhoMaximo) return Erro("O XML passa de 2 MB.");

        string xml;
        NfeLida nfe;
        try
        {
            await using var conteudo = arquivo.OpenReadStream();
            using var copia = new MemoryStream();
            await conteudo.CopyToAsync(copia, ct);
            xml = Encoding.UTF8.GetString(copia.ToArray());
            copia.Position = 0;
            nfe = LeitorDeNfe.Ler(copia);
        }
        catch (NfeInvalidaException ex)
        {
            return Erro(ex.Message);
        }

        var cnpjDaEmpresa = Somente(empresa.Cnpj);
        if (nfe.DestinatarioDocumento is { } destino && Somente(destino) != cnpjDaEmpresa)
            return Erro($"Esta nota foi emitida para o CNPJ {Formatar(destino)}, não para {empresa.Nome} ({empresa.Cnpj}). Troque a empresa no topo da tela.");

        var anteriores = await db.Notas.Where(n => n.Chave == nfe.Chave).ToListAsync(ct);
        if (anteriores.FirstOrDefault(n => n.Status != StatusDaNota.Descartada) is { } existente)
            return Permissoes.Problema(StatusCodes.Status409Conflict,
                $"A NF-e {existente.Numero} de {existente.FornecedorNome} já foi importada ({(existente.Status == StatusDaNota.Lancada ? "lançada" : "em conferência")}).");
        db.Notas.RemoveRange(anteriores); // descartadas: a nova importação substitui

        var nota = NotaDeEntrada.Importar(empresa.EmpresaId!.Value, nfe, xml, relogio.GetUtcNow(), usuario.Nome);
        await VincularAutomaticamenteAsync(nota, db, catalogo, ct);
        db.Notas.Add(nota);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/estoque/notas/{nota.Id}", await ParaDtoAsync(nota, db, catalogo, fornecedores, ct));
    }

    /// <summary>De-para do fornecedor primeiro; depois o EAN do item igual ao código de barras de um SKU.</summary>
    private static async Task VincularAutomaticamenteAsync(NotaDeEntrada nota, EstoqueDbContext db, ICatalogoDeProdutos catalogo, CancellationToken ct)
    {
        var vinculos = await db.Vinculos.AsNoTracking().Where(v => v.FornecedorDocumento == nota.FornecedorDocumento).ToDictionaryAsync(v => v.Codigo, ct);
        var skusDoDePara = (await catalogo.SkusAsync(vinculos.Values.Where(v => v.SkuId is not null).Select(v => v.SkuId!.Value).ToList(), ct)).ToDictionary(s => s.Id);
        foreach (var item in nota.Itens)
        {
            if (vinculos.TryGetValue(item.Codigo, out var v))
            {
                if (v.SkuId is { } skuId && skusDoDePara.TryGetValue(skuId, out var sku) && sku.Ativo && TentarUmSku(item, sku.ProdutoId, skuId, v.Fator, "de-para")) continue;
                if (await catalogo.ProdutoAsync(v.ProdutoId, ct) is not null)
                {
                    item.Sugerir(v.ProdutoId, v.Fator, "de-para");
                    continue;
                }
            }
            if (item.Ean is { } ean && await catalogo.SkuPorCodigoAsync(ean, ct) is { Ativo: true } porEan) TentarUmSku(item, porEan.ProdutoId, porEan.Id, 1, "ean");
        }
    }

    private static bool TentarUmSku(ItemDaNota item, Guid produtoId, Guid skuId, int fator, string origem)
    {
        var pecas = item.Quantidade * fator;
        if (pecas % 1 != 0 || pecas <= 0) return false;
        item.Distribuir(produtoId, fator, [(skuId, (int)pecas)], origem);
        return true;
    }

    private static string Somente(string? documento) => new((documento ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());

    private static string Formatar(string documento) => Cnpj.TryParse(documento, out var cnpj) ? cnpj.Formatado : documento;

    private static async Task<IResult> Obter(Guid id, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IDiretorioDeFornecedores fornecedores,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var nota = await CarregarAsync(db, empresa, id, ct);
        return nota is null ? Results.NotFound() : Results.Ok(await ParaDtoAsync(nota, db, catalogo, fornecedores, ct));
    }

    private static async Task<IResult> ConferirItem(Guid id, int numero, ItemRequest r, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var nota = await CarregarAsync(db, empresa, id, ct);
        var item = nota?.Itens.FirstOrDefault(i => i.Numero == numero);
        if (item is null) return Results.NotFound();
        if (nota!.Status != StatusDaNota.Pendente) return Permissoes.Problema(StatusCodes.Status409Conflict, "Esta nota já foi lançada ou descartada.");

        if (r.Ignorado == true) item.Ignorar(true);
        else if (r.Distribuicao is { Count: > 0 } distribuicao)
        {
            var produto = r.ProdutoId is { } produtoId ? await catalogo.ProdutoAsync(produtoId, ct) : null;
            if (produto is null) return Erro("Escolha o produto do catálogo.");
            var doProduto = produto.Skus.ToDictionary(s => s.Id);
            if (distribuicao.Any(d => !doProduto.ContainsKey(d.SkuId))) return Erro("Há SKUs que não são deste produto.");
            if (distribuicao.FirstOrDefault(d => !doProduto[d.SkuId].Ativo) is { } inativo)
                return Erro($"{doProduto[inativo.SkuId].Cor} {doProduto[inativo.SkuId].Tamanho} está desativado no cadastro do produto.");
            try
            {
                foreach (var nova in item.Distribuir(produto.Id, r.Fator ?? 1, distribuicao.Select(d => (d.SkuId, d.Quantidade)).ToList(), null))
                    db.Distribuicoes.Add(nova);
            }
            catch (ArgumentException ex)
            {
                return Erro(ex.Message.Split(" (Parameter")[0]);
            }
        }
        else item.Ignorar(false);

        await db.SaveChangesAsync(ct);
        var skus = (await catalogo.SkusAsync(item.Distribuicao.Select(d => d.SkuId).ToList(), ct)).ToDictionary(s => s.Id);
        var produtos = new Dictionary<Guid, string>();
        if (item.ProdutoId is { } pid && await catalogo.ProdutoAsync(pid, ct) is { } p) produtos[pid] = $"{p.Referencia} — {p.Descricao}";
        return Results.Ok(ItemParaDto(item, skus, produtos));
    }

    private static async Task<IResult> Lancar(Guid id, LancarRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo,
        IDiretorioDeFornecedores fornecedores, IContasAPagar contasAPagar, IEmpresaAtiva empresa, IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var nota = await CarregarAsync(db, empresa, id, ct);
        if (nota is null) return Results.NotFound();
        if (nota.Status != StatusDaNota.Pendente) return Permissoes.Problema(StatusCodes.Status409Conflict, "Esta nota já foi lançada ou descartada.");
        if (!nota.ProntaParaLancar)
            return Permissoes.Problema(StatusCodes.Status409Conflict, "Ainda há itens sem produto. Vincule cada item a SKU(s) da grade ou marque como ignorado.");
        var loja = await lojas.DaEmpresaAtivaAsync(r.LojaId, ct);
        if (loja is null) return Results.NotFound();
        if (!loja.Ativa) return Erro($"{loja.Nome} está desativada.");

        var prontos = nota.Itens.Where(i => i.Situacao == SituacaoDoItem.Pronto).ToList();
        var skus = (await catalogo.SkusAsync(prontos.SelectMany(i => i.Distribuicao.Select(d => d.SkuId)).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (prontos.SelectMany(i => i.Distribuicao).FirstOrDefault(d => !skus.TryGetValue(d.SkuId, out var s) || !s.Ativo) is { } problema)
            return Erro(skus.TryGetValue(problema.SkuId, out var s)
                ? $"{Saldos.Nome(s)} está desativado no cadastro. Reative ou troque o SKU do item."
                : "Um SKU vinculado não existe mais no catálogo. Confira os itens.");

        var e = LeitorDeNfe.Ler(new MemoryStream(Encoding.UTF8.GetBytes(nota.Xml))).Emitente;
        var fornecedor = await fornecedores.GarantirAsync(new NovoFornecedor(e.Documento, e.RazaoSocial, e.NomeFantasia, e.Telefone, e.Cep, e.Logradouro,
            e.Numero, e.Bairro, e.Cidade, e.Uf), ct);

        var agora = relogio.GetUtcNow();
        var op = new Operacao(Guid.NewGuid(), agora, usuario.Id, usuario.Nome, null, $"NF {nota.Numero}/{nota.Serie}", $"Fornecedor {nota.FornecedorNome}", null);
        var saldos = await Saldos.DaLojaAsync(db, loja.Id, skus.Values, ct);
        var movimentos = prontos.SelectMany(i => i.Distribuicao.Select(d => saldos[d.SkuId].Entrar(TipoDeMovimento.Entrada, d.Quantidade, i.CustoPorPeca, op))).ToList();
        db.Movimentos.AddRange(movimentos);
        await MemorizarDeParaAsync(db, nota, prontos, agora, ct);
        nota.MarcarLancada(loja.Id, op.Id, fornecedor.Id, agora, usuario.Nome);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, "O estoque mudou enquanto a nota era lançada. Tente de novo.");
        }

        // Duplicatas → contas a pagar (nota sem cobrança: à vista, vence na emissão). Registrar de novo não duplica (chave da NF-e).
        var emissao = DateOnly.FromDateTime(nota.EmitidaEm.ToOffset(TimeSpan.FromHours(-3)).DateTime);
        var parcelas = nota.Duplicatas.Count > 0
            ? nota.Duplicatas.OrderBy(d => d.Vencimento).Select(d => new ParcelaAPagar(d.Vencimento, d.Valor)).ToList()
            : [new ParcelaAPagar(emissao, nota.ValorTotal)];
        await contasAPagar.RegistrarCompraAsync(new ContaAPagarDeCompra(nota.EmpresaId, "NotaDeEntrada", nota.Chave, $"NF {nota.Numero}/{nota.Serie} — {nota.FornecedorNome}",
            fornecedor.Id, fornecedor.Nome, $"NF-e {nota.Numero}", emissao, parcelas), ct);
        return Results.Ok(new MovimentacoesEndpoints.OperacaoDto(op.Id, movimentos.Count, movimentos.Sum(m => m.Quantidade)));
    }

    /// <summary>Um SKU só = de-para completo (código → SKU e fator); grade distribuída = lembra só o produto e o fator.</summary>
    private static async Task MemorizarDeParaAsync(EstoqueDbContext db, NotaDeEntrada nota, List<ItemDaNota> prontos, DateTimeOffset agora, CancellationToken ct)
    {
        var codigos = prontos.Select(i => i.Codigo).Distinct().ToList();
        var existentes = await db.Vinculos.Where(v => v.FornecedorDocumento == nota.FornecedorDocumento && codigos.Contains(v.Codigo)).ToDictionaryAsync(v => v.Codigo, ct);
        foreach (var item in prontos.GroupBy(i => i.Codigo).Select(g => g.First()))
        {
            var skuId = item.Distribuicao.Count == 1 ? item.Distribuicao.First().SkuId : (Guid?)null;
            if (existentes.TryGetValue(item.Codigo, out var vinculo)) vinculo.Atualizar(item.ProdutoId!.Value, skuId, item.Fator, agora);
            else db.Vinculos.Add(VinculoDeProduto.Criar(nota.FornecedorDocumento, item.Codigo, item.ProdutoId!.Value, skuId, item.Fator, agora));
        }
    }

    private static async Task<IResult> Descartar(Guid id, EstoqueDbContext db, IEmpresaAtiva empresa, CancellationToken ct)
    {
        if (Barrar(empresa) is { } negado) return negado;
        var nota = await CarregarAsync(db, empresa, id, ct);
        if (nota is null) return Results.NotFound();
        if (nota.Status != StatusDaNota.Pendente) return Permissoes.Problema(StatusCodes.Status409Conflict, "Esta nota já foi lançada ou descartada.");
        nota.Descartar();
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
