using System.Globalization;
using Kamus.Modules.Vendas.Dominio;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.SharedKernel.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Vendas.Aplicacao;

/// <param name="ValeCodigo">Obrigatório quando a forma é vale-troca.</param>
public sealed record PagamentoPedido(Guid FormaId, decimal Valor, int Parcelas, string? ValeCodigo = null);

/// <summary>Um pagamento já gravado (de venda ou de troca), para gerar os recebíveis.</summary>
internal sealed record PagamentoGravado(Guid FormaId, string FormaNome, TipoDePagamento Tipo, decimal Valor, int Parcelas, decimal TaxaPercentual, decimal ValorTaxa);

/// <summary>Resolve as formas de pagamento pedidas (validade, parcelas, conta, vale-troca) e monta os recebíveis do financeiro.</summary>
internal static class Pagamentos
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Valida os pedidos e devolve os pagamentos do domínio. O saldo dos vales usados é debitado nas entidades rastreadas pelo
    /// <paramref name="db"/> — só fica gravado se quem chama salvar.
    /// </summary>
    public static async Task<(List<PagamentoNovo> Pagamentos, Dictionary<Guid, FormaDePagamento> Formas, string? Erro)> ResolverAsync(
        VendasDbContext db, IContasAReceber financeiro, ICrediario crediario, Guid empresaId, Guid? clienteId, IReadOnlyList<PagamentoPedido> pedidos, DateOnly hoje,
        CancellationToken ct)
    {
        var formaIds = pedidos.Select(p => p.FormaId).Distinct().ToList();
        var formas = await db.Formas.AsNoTracking().Where(f => formaIds.Contains(f.Id) && f.EmpresaId == empresaId).ToDictionaryAsync(f => f.Id, ct);
        var resultado = new List<PagamentoNovo>();
        foreach (var p in pedidos)
        {
            if (!formas.TryGetValue(p.FormaId, out var forma)) return ([], formas, "Forma de pagamento não encontrada.");
            if (!forma.Ativa) return ([], formas, $"{forma.Nome} está desativada.");
            if (p.Parcelas > forma.MaxParcelas)
                return ([], formas, forma.MaxParcelas == 1 ? $"{forma.Nome} não parcela." : $"{forma.Nome} aceita até {forma.MaxParcelas} parcelas.");
            if (forma.EntraNaHora && (forma.ContaId is not { } conta || !await financeiro.ContaValidaAsync(empresaId, conta, ct)))
                return ([], formas, $"Configure a conta financeira de {forma.Nome} em Formas de pagamento.");

            Guid? valeId = null;
            if (forma.Tipo == TipoDePagamento.ValeTroca)
            {
                var codigo = p.ValeCodigo?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(codigo)) return ([], formas, "Informe o código do vale-troca.");
                var vale = await db.Vales.SingleOrDefaultAsync(v => v.Codigo == codigo && v.EmpresaId == empresaId, ct);
                if (vale is null) return ([], formas, $"Vale-troca {codigo} não encontrado nesta empresa.");
                try
                {
                    vale.Usar(p.Valor, hoje);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    return ([], formas, ex.Message.Split(" (Parameter")[0]);
                }
                valeId = vale.Id;
            }
            resultado.Add(new PagamentoNovo(forma.Id, forma.Nome, forma.Tipo, p.Valor, p.Parcelas, forma.Taxa(p.Parcelas), valeId));
        }
        var noCrediario = resultado.Where(p => p.Tipo == TipoDePagamento.Crediario).Sum(p => p.Valor);
        if (noCrediario > 0)
        {
            if (clienteId is not { } cliente) return ([], formas, "Venda no crediário precisa de cliente cadastrado: escolha o cliente.");
            var situacao = await crediario.SituacaoAsync(cliente, ct);
            if (situacao.Bloqueado) return ([], formas, situacao.Motivo);
            if (noCrediario > situacao.Disponivel)
                return ([], formas, $"Limite disponível no crediário: R$ {Dinheiro.Formatar(situacao.Disponivel)} (a compra pede R$ {Dinheiro.Formatar(noCrediario)}).");
        }
        return (resultado, formas, null);
    }

    /// <summary>Dinheiro/PIX entram baixados na conta da forma; cartões e outros ficam a receber (líquidos da taxa); crediário vira dívida do cliente; vale-troca não gera nada.</summary>
    public static ContasAReceberDeVenda Recebiveis(Guid empresaId, string origem, Guid origemId, string documento, Guid? clienteId, string? clienteNome, DateOnly data,
        IEnumerable<PagamentoGravado> pagamentos, IReadOnlyDictionary<Guid, FormaDePagamento> formas) =>
        new(empresaId, origem, origemId.ToString(), clienteId, clienteNome, data,
            pagamentos.Where(p => p.Tipo != TipoDePagamento.ValeTroca).Select(p =>
            {
                var forma = formas[p.FormaId];
                var descricao = $"{documento} — {p.FormaNome}{(p.Parcelas > 1 ? $" {p.Parcelas}x" : "")}";
                if (forma.Tipo == TipoDePagamento.Crediario)
                    return new RecebimentoDeVenda(descricao, forma.Recebimentos(p.Valor, p.Parcelas, data), null, JaRecebido: false, "Crediário próprio.", Crediario: true);
                return forma.EntraNaHora
                    ? new RecebimentoDeVenda(descricao, [new ParcelaAReceber(data, p.Valor)], forma.ContaId, JaRecebido: true, null)
                    : new RecebimentoDeVenda(descricao, forma.Recebimentos(p.Valor, p.Parcelas, data), forma.ContaId, JaRecebido: false,
                        p.ValorTaxa > 0 ? $"Valor bruto R$ {Dinheiro.Formatar(p.Valor)}, taxa {p.TaxaPercentual.ToString("0.##", PtBr)}% (R$ {Dinheiro.Formatar(p.ValorTaxa)})." : null);
            }).ToList());
}
