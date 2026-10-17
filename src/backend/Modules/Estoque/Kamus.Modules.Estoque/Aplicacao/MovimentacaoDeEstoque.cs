using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Aplicacao;

/// <summary>Baixa de estoque das vendas e devolução no cancelamento (contrato do SharedKernel para o módulo de vendas).</summary>
internal sealed class MovimentacaoDeEstoque(EstoqueDbContext db, ICatalogoDeProdutos catalogo, IUsuarioAtual usuario, TimeProvider relogio) : IMovimentacaoDeEstoque
{
    public async Task<SaidaRegistrada> RegistrarVendaAsync(SaidaDeEstoque saida, CancellationToken ct = default)
    {
        var loja = await db.Lojas.SingleOrDefaultAsync(l => l.Id == saida.LojaId && l.EmpresaId == saida.EmpresaId, ct)
            ?? throw new SaidaDeEstoqueRecusadaException("Loja não encontrada nesta empresa.");
        if (!loja.Ativa) throw new SaidaDeEstoqueRecusadaException($"{loja.Nome} está desativada.");
        if (saida.Itens.Count == 0 || saida.Itens.Any(i => i.Quantidade <= 0)) throw new SaidaDeEstoqueRecusadaException("Informe as quantidades vendidas.");

        var itens = saida.Itens.GroupBy(i => i.SkuId).Select(g => new ItemDeSaida(g.Key, g.Sum(i => i.Quantidade))).ToList();
        var skus = (await catalogo.SkusAsync(itens.Select(i => i.SkuId).ToList(), ct)).ToDictionary(s => s.Id);
        if (skus.Count != itens.Count) throw new SaidaDeEstoqueRecusadaException("Há itens que não existem no catálogo.");

        var saldos = await Saldos.DaLojaAsync(db, loja.Id, skus.Values, ct);
        var op = new Operacao(Guid.NewGuid(), relogio.GetUtcNow(), usuario.Id, usuario.Nome, null, saida.Documento, null, null);
        var movimentos = new List<MovimentoDeEstoque>();
        foreach (var item in itens)
        {
            try
            {
                movimentos.Add(saldos[item.SkuId].Vender(item.Quantidade, op, saida.PermitirNegativo));
            }
            catch (EstoqueInsuficienteException ex)
            {
                throw new SaidaDeEstoqueRecusadaException($"{Saldos.Nome(skus[item.SkuId])}: há {ex.Disponivel} em estoque e a venda pede {ex.Pedido}.");
            }
        }
        db.Movimentos.AddRange(movimentos);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new SaidaDeEstoqueRecusadaException("O estoque mudou enquanto a venda era registrada. Tente de novo.");
        }
        return new SaidaRegistrada(op.Id, movimentos.Select(m => new ItemSaido(m.SkuId, -m.Quantidade, m.CustoUnitario, m.SaldoApos)).ToList());
    }

    public async Task EstornarVendaAsync(Guid operacaoId, string documento, CancellationToken ct = default)
    {
        var saidas = await db.Movimentos.Where(m => m.OperacaoId == operacaoId && m.Tipo == TipoDeMovimento.Venda).ToListAsync(ct);
        if (saidas.Count == 0) return;
        var origem = operacaoId.ToString();
        if (await db.Movimentos.AnyAsync(m => m.Tipo == TipoDeMovimento.EstornoDeVenda && m.Observacao == origem, ct)) return; // já devolvido

        var op = new Operacao(Guid.NewGuid(), relogio.GetUtcNow(), usuario.Id, usuario.Nome, "Cancelamento de venda", documento, origem, null);
        foreach (var porLoja in saidas.GroupBy(m => m.LojaId))
        {
            var skus = (await catalogo.SkusAsync(porLoja.Select(m => m.SkuId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
            var saldos = await Saldos.DaLojaAsync(db, porLoja.Key, skus.Values, ct);
            foreach (var m in porLoja.Where(m => saldos.ContainsKey(m.SkuId)))
                db.Movimentos.Add(saldos[m.SkuId].Entrar(TipoDeMovimento.EstornoDeVenda, -m.Quantidade, m.CustoUnitario, op));
        }
        await db.SaveChangesAsync(ct);
    }
}
