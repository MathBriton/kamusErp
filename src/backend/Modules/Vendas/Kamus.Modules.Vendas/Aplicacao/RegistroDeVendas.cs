using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

/// <param name="Erro">Resposta de erro pronta (ProblemDetails) quando a venda é recusada.</param>
internal sealed record ResultadoDaVenda(Venda? Venda, IReadOnlyList<string> Avisos, IResult? Erro);

/// <summary>
/// Registra uma venda: preço do catálogo, vendedor e comissão, formas de pagamento (vale, crediário), baixa de estoque e recebíveis.
/// Usado pelo registro de venda e pelo fechamento da condicional.
/// </summary>
internal sealed class RegistroDeVendas(VendasDbContext db, IEmpresaAtiva empresa, IUsuarioAtual usuario, ICatalogoDeProdutos catalogo,
    IDiretorioDeVendedores vendedores, IMovimentacaoDeEstoque estoque, IContasAReceber financeiro, ICrediario crediario, TimeProvider relogio)
{
    private static ResultadoDaVenda Falha(IResult erro) => new(null, [], erro);

    public async Task<ResultadoDaVenda> RegistrarAsync(VendasEndpoints.VendaRequest r, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId!.Value;
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var data = r.Data ?? hoje;
        if (data > hoje) return Falha(Permissoes.Erro("A data da venda não pode ser futura."));
        var itens = r.Itens ?? [];
        var pedidos = r.Pagamentos ?? [];
        if (pedidos.Count == 0) return Falha(Permissoes.Erro("Informe ao menos uma forma de pagamento."));

        VendedorResumo? vendedor = null;
        if (r.VendedorId is { } vendedorId)
        {
            vendedor = await vendedores.ObterAsync(vendedorId, ct);
            if (vendedor is null) return Falha(Permissoes.Erro("Vendedor não encontrado."));
            if (!vendedor.Ativo) return Falha(Permissoes.Erro($"{vendedor.Nome} está inativo."));
        }

        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        if (itens.Any(i => !skus.ContainsKey(i.SkuId))) return Falha(Permissoes.Erro("Há itens que não existem no catálogo."));
        if (itens.FirstOrDefault(i => !skus[i.SkuId].Ativo) is { } inativo) return Falha(Permissoes.Erro($"{Nome(skus[inativo.SkuId])} está inativo no catálogo."));

        var (pagamentos, formas, erro) = await Pagamentos.ResolverAsync(db, financeiro, crediario, empresaId, r.ClienteId, pedidos, hoje, ct);
        if (erro is not null) return Falha(Permissoes.Erro(erro));

        var numero = (await db.Vendas.Where(v => v.EmpresaId == empresaId).MaxAsync(v => (int?)v.Numero, ct) ?? 0) + 1;
        Venda venda;
        try
        {
            venda = Venda.Criar(
                new DadosDaVenda(empresaId, r.LojaId, numero, data, r.ClienteId, r.ClienteNome, vendedor?.Id, vendedor?.Nome, vendedor?.ComissaoPercentual ?? 0,
                    r.NotaExterna, r.Observacao, usuario.Id),
                itens.Select(i => new ItemNovo(i.SkuId, skus[i.SkuId].ProdutoId, Nome(skus[i.SkuId]), i.Quantidade, skus[i.SkuId].PrecoVenda, i.Desconto)).ToList(),
                r.DescontoGeral, pagamentos, usuario.Nome, relogio.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return Falha(Permissoes.Erro(Permissoes.Mensagem(ex)));
        }

        var documento = $"Venda nº {numero}";
        var configuracao = await FormasEndpoints.ConfiguracaoAsync(db, empresaId, ct);
        SaidaRegistrada saida;
        try
        {
            saida = await estoque.RegistrarVendaAsync(new SaidaDeEstoque(empresaId, r.LojaId, documento,
                venda.Itens.Select(i => new ItemDeSaida(i.SkuId, i.Quantidade)).ToList(), configuracao.PermitirEstoqueNegativo), ct);
        }
        catch (SaidaDeEstoqueRecusadaException ex)
        {
            return Falha(Permissoes.Problema(StatusCodes.Status409Conflict, ex.Message));
        }
        venda.RegistrarBaixaDeEstoque(saida.OperacaoId, saida.Itens.ToDictionary(i => i.SkuId, i => i.CustoUnitario));

        try
        {
            await financeiro.RegistrarVendaAsync(Pagamentos.Recebiveis(venda.EmpresaId, VendasEndpoints.Origem, venda.Id, documento, venda.ClienteId, venda.ClienteNome, venda.Data,
                venda.Pagamentos.Select(VendasEndpoints.Gravado), formas), ct);
            db.Vendas.Add(venda);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sem transação entre módulos: desfaz o que já foi gravado nos outros contextos.
            await estoque.EstornarVendaAsync(saida.OperacaoId, $"{documento} não concluída", CancellationToken.None);
            await financeiro.CancelarOrigemAsync(VendasEndpoints.Origem, venda.Id.ToString(), usuario.Nome, CancellationToken.None);
            throw;
        }

        var avisos = saida.Itens.Where(i => i.SaldoApos < 0)
            .Select(i => $"{Nome(skus[i.SkuId])} ficou com estoque {i.SaldoApos}.").ToList();
        return new ResultadoDaVenda(venda, avisos, null);
    }

    private static string Nome(SkuDoCatalogo sku) => $"{sku.Descricao} {sku.Cor} {sku.Tamanho}";
}
