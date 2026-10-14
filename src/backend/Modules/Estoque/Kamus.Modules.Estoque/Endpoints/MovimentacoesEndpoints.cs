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

/// <summary>Entradas, ajustes e transferências. Cada requisição é uma operação: grava tudo ou nada.</summary>
internal static class MovimentacoesEndpoints
{
    public const string CodigoInsuficiente = "ESTOQUE_INSUFICIENTE";

    /// <param name="CustoUnitario">Só na entrada. Vazio = custo do cadastro do produto.</param>
    public sealed record ItemRequest(Guid SkuId, int Quantidade, decimal? CustoUnitario);
    public sealed record EntradaRequest(Guid LojaId, string? Documento, string? Observacao, IReadOnlyList<ItemRequest>? Itens);

    /// <param name="Motivo">Perda, Avaria, Furto, Correcao ou Outro (este exige observação).</param>
    public sealed record AjusteRequest(Guid LojaId, string? Motivo, string? Observacao, IReadOnlyList<ItemRequest>? Itens);
    public sealed record TransferenciaRequest(Guid OrigemId, Guid DestinoId, string? Observacao, IReadOnlyList<ItemRequest>? Itens);
    public sealed record OperacaoDto(Guid OperacaoId, int Itens, int Quantidade);

    public static readonly IReadOnlyDictionary<string, string> Motivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Perda"] = "Perda",
        ["Avaria"] = "Avaria",
        ["Furto"] = "Furto",
        ["Correcao"] = "Correção de contagem",
        ["Outro"] = "Outro",
    };

    public static IEndpointRouteBuilder MapMovimentacoes(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/estoque").RequireAuthorization().WithTags("Estoque");
        g.MapPost("/entradas", Entrada).WithSummary("Entrada de mercadoria (soma o saldo e recalcula o custo médio)")
            .Produces<OperacaoDto>().ProducesProblem(StatusCodes.Status400BadRequest);
        g.MapPost("/ajustes", Ajuste).WithSummary("Ajuste com motivo: quantidade negativa baixa, positiva soma (pelo custo médio)")
            .Produces<OperacaoDto>().ProducesProblem(StatusCodes.Status409Conflict);
        g.MapPost("/transferencias", Transferencia).WithSummary("Transferência entre lojas da empresa (leva o custo médio da origem)")
            .Produces<OperacaoDto>().ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    private static IResult Erro(string detalhe) => Permissoes.Problema(StatusCodes.Status400BadRequest, detalhe);

    private static string? Texto(string? valor, int maximo) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, maximo)];

    /// <summary>Confere os itens e devolve os SKUs do catálogo (ou o erro para a tela).</summary>
    private static async Task<(Dictionary<Guid, SkuDoCatalogo> Skus, IResult? Erro)> ItensAsync(
        IReadOnlyList<ItemRequest>? itens, ICatalogoDeProdutos catalogo, bool permiteNegativa, CancellationToken ct)
    {
        if (itens is null || itens.Count == 0) return ([], Erro("Informe ao menos um item."));
        if (itens.Count > 2000) return ([], Erro("No máximo 2.000 itens por operação."));
        if (itens.GroupBy(i => i.SkuId).Any(g => g.Count() > 1)) return ([], Erro("O mesmo SKU aparece mais de uma vez. Some as quantidades numa linha só."));
        if (itens.Any(i => i.Quantidade == 0 || (!permiteNegativa && i.Quantidade < 0)))
            return ([], Erro(permiteNegativa ? "As quantidades não podem ser zero." : "As quantidades precisam ser maiores que zero."));
        if (itens.Any(i => i.CustoUnitario < 0)) return ([], Erro("O custo não pode ser negativo."));
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).ToList(), ct)).ToDictionary(s => s.Id);
        return skus.Count == itens.Count ? (skus, null) : ([], Erro("Há itens que não existem no catálogo. Atualize a tela e tente de novo."));
    }

    private static Operacao NovaOperacao(IUsuarioAtual usuario, TimeProvider relogio, string? motivo, string? documento, string? observacao, Guid? relacionada = null) =>
        new(Guid.NewGuid(), relogio.GetUtcNow(), usuario.Id, usuario.Nome, motivo, Texto(documento, 60), Texto(observacao, 500), relacionada);

    /// <summary>Grava a operação; saldo insuficiente ou concorrência viram 409 sem gravar nada.</summary>
    private static async Task<IResult> GravarAsync(EstoqueDbContext db, Operacao op, Func<IEnumerable<MovimentoDeEstoque>> movimentar,
        Dictionary<Guid, SkuDoCatalogo> skus, CancellationToken ct)
    {
        List<MovimentoDeEstoque> movimentos;
        try
        {
            movimentos = movimentar().ToList();
        }
        catch (EstoqueInsuficienteEmException ex)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict,
                $"{Saldos.Nome(skus[ex.SkuId])}: há {ex.Erro.Disponivel} e a operação pede {ex.Erro.Pedido}.", CodigoInsuficiente);
        }
        db.Movimentos.AddRange(movimentos);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Permissoes.Problema(StatusCodes.Status409Conflict, "O estoque mudou enquanto você lançava. Confira os saldos e tente de novo.");
        }
        var principais = movimentos.Where(m => m.Tipo != TipoDeMovimento.TransferenciaEntrada).ToList();
        return Results.Ok(new OperacaoDto(op.Id, principais.Count, principais.Sum(m => Math.Abs(m.Quantidade))));
    }

    private sealed class EstoqueInsuficienteEmException(Guid skuId, EstoqueInsuficienteException erro) : Exception(erro.Message, erro)
    {
        public Guid SkuId { get; } = skuId;
        public EstoqueInsuficienteException Erro { get; } = erro;
    }

    private static MovimentoDeEstoque NoSku(Guid skuId, Func<MovimentoDeEstoque> acao)
    {
        try
        {
            return acao();
        }
        catch (EstoqueInsuficienteException ex)
        {
            throw new EstoqueInsuficienteEmException(skuId, ex);
        }
    }

    private static async Task<IResult> Entrada(EntradaRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IEmpresaAtiva empresa,
        IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (!Permissoes.PodeMovimentar(empresa.Perfil)) return Permissoes.Negado("Só Dono, Gerente e Estoquista lançam entradas.");
        var loja = await lojas.DaEmpresaAtivaAsync(r.LojaId, ct);
        if (loja is null) return Results.NotFound();
        if (!loja.Ativa) return Erro($"{loja.Nome} está desativada.");
        var (skus, erro) = await ItensAsync(r.Itens, catalogo, permiteNegativa: false, ct);
        if (erro is not null) return erro;
        if (skus.Values.FirstOrDefault(s => !s.Ativo) is { } inativo) return Erro($"{Saldos.Nome(inativo)} está desativado no cadastro do produto.");

        var saldos = await Saldos.DaLojaAsync(db, loja.Id, skus.Values, ct);
        var op = NovaOperacao(usuario, relogio, null, r.Documento, r.Observacao);
        return await GravarAsync(db, op, () => r.Itens!.Select(i =>
            saldos[i.SkuId].Entrar(TipoDeMovimento.Entrada, i.Quantidade, i.CustoUnitario ?? skus[i.SkuId].PrecoCusto ?? 0, op)), skus, ct);
    }

    private static async Task<IResult> Ajuste(AjusteRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IEmpresaAtiva empresa,
        IUsuarioAtual usuario, IRegistradorDeEventos eventos, TimeProvider relogio, CancellationToken ct)
    {
        if (!Permissoes.PodeMovimentar(empresa.Perfil)) return Permissoes.Negado("Só Dono, Gerente e Estoquista ajustam o estoque.");
        if (r.Motivo is null || !Motivos.TryGetValue(r.Motivo, out var motivo)) return Erro("Escolha o motivo do ajuste: perda, avaria, furto, correção de contagem ou outro.");
        if (motivo == "Outro" && string.IsNullOrWhiteSpace(r.Observacao)) return Erro("Descreva o motivo na observação.");
        var loja = await lojas.DaEmpresaAtivaAsync(r.LojaId, ct);
        if (loja is null) return Results.NotFound();
        var (skus, erro) = await ItensAsync(r.Itens, catalogo, permiteNegativa: true, ct);
        if (erro is not null) return erro;

        var saldos = await Saldos.DaLojaAsync(db, loja.Id, skus.Values, ct);
        var op = NovaOperacao(usuario, relogio, motivo, null, r.Observacao);
        var resultado = await GravarAsync(db, op, () => r.Itens!.Select(i => NoSku(i.SkuId, () =>
        {
            var saldo = saldos[i.SkuId];
            if (i.Quantidade < 0) return saldo.Sair(TipoDeMovimento.Ajuste, -i.Quantidade, op);
            var custo = saldo.Quantidade > 0 ? saldo.CustoMedio : skus[i.SkuId].PrecoCusto ?? saldo.CustoMedio;
            return saldo.Entrar(TipoDeMovimento.Ajuste, i.Quantidade, custo, op);
        })), skus, ct);
        if (resultado is IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK })
        {
            var (saiu, entrou) = (r.Itens!.Where(i => i.Quantidade < 0).Sum(i => -i.Quantidade), r.Itens!.Where(i => i.Quantidade > 0).Sum(i => i.Quantidade));
            await eventos.RegistrarAsync("ajuste_de_estoque",
                $"Ajuste de estoque ({motivo}) em {loja.Nome}: {r.Itens!.Count} SKU(s), saíram {saiu} e entraram {entrou} peça(s)", ct: ct);
        }
        return resultado;
    }

    private static async Task<IResult> Transferencia(TransferenciaRequest r, Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo, IEmpresaAtiva empresa,
        IUsuarioAtual usuario, TimeProvider relogio, CancellationToken ct)
    {
        if (!Permissoes.PodeMovimentar(empresa.Perfil)) return Permissoes.Negado("Só Dono, Gerente e Estoquista transferem estoque.");
        if (r.OrigemId == r.DestinoId) return Erro("Escolha lojas de origem e destino diferentes.");
        var origem = await lojas.DaEmpresaAtivaAsync(r.OrigemId, ct);
        var destino = await lojas.DaEmpresaAtivaAsync(r.DestinoId, ct);
        if (origem is null || destino is null) return Results.NotFound();
        if (!destino.Ativa) return Erro($"{destino.Nome} está desativada.");
        var (skus, erro) = await ItensAsync(r.Itens, catalogo, permiteNegativa: false, ct);
        if (erro is not null) return erro;

        var deOrigem = await Saldos.DaLojaAsync(db, origem.Id, skus.Values, ct);
        var deDestino = await Saldos.DaLojaAsync(db, destino.Id, skus.Values, ct);
        var saida = NovaOperacao(usuario, relogio, null, null, r.Observacao, destino.Id);
        var entrada = saida with { LojaRelacionadaId = origem.Id };
        return await GravarAsync(db, saida, () => r.Itens!.SelectMany(i =>
        {
            var custo = deOrigem[i.SkuId].CustoMedio;
            var saiu = NoSku(i.SkuId, () => deOrigem[i.SkuId].Sair(TipoDeMovimento.TransferenciaSaida, i.Quantidade, saida));
            return new[] { saiu, deDestino[i.SkuId].Entrar(TipoDeMovimento.TransferenciaEntrada, i.Quantidade, custo, entrada) };
        }), skus, ct);
    }
}
