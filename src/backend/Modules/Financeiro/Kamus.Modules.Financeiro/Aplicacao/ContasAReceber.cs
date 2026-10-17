using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <summary>Recebíveis das vendas: um título por forma de pagamento (categoria "Vendas"); dinheiro/PIX já entram baixados na conta.</summary>
internal sealed class ContasAReceber(FinanceiroDbContext db, TimeProvider relogio) : IContasAReceber
{
    public const string CategoriaDeVendas = "Vendas";

    public Task<bool> ContaValidaAsync(Guid empresaId, Guid contaId, CancellationToken ct = default) =>
        db.Contas.AnyAsync(c => c.Id == contaId && c.EmpresaId == empresaId && c.Ativa, ct);

    public async Task RegistrarVendaAsync(ContasAReceberDeVenda venda, CancellationToken ct = default)
    {
        if (await db.Titulos.AnyAsync(t => t.Origem == venda.Origem && t.OrigemId == venda.OrigemId, ct)) return;

        var chave = TextoParaBusca.Normalizar(CategoriaDeVendas);
        var categoria = (await db.Categorias.Where(c => c.Tipo == TipoDeCategoria.Receita).ToListAsync(ct)).FirstOrDefault(c => TextoParaBusca.Normalizar(c.Nome) == chave);
        if (categoria is null)
        {
            categoria = Categoria.Criar(CategoriaDeVendas, "Vendas", TipoDeCategoria.Receita);
            db.Categorias.Add(categoria);
        }

        var agora = relogio.GetUtcNow();
        foreach (var r in venda.Recebimentos)
        {
            var titulo = Titulo.Criar(venda.EmpresaId,
                new DadosDoTitulo(TipoDeTitulo.Receber, r.Descricao, venda.ClienteId, venda.ClienteNome, categoria.Id, null, venda.Data, r.Observacao),
                r.Parcelas.Select(p => new NovaParcela(p.Vencimento, p.Valor)).ToList(), agora, venda.Origem, venda.OrigemId);
            db.Titulos.Add(titulo);
            if (!r.JaRecebido) continue;
            var conta = r.ContaId is { } id ? await db.Contas.SingleOrDefaultAsync(c => c.Id == id && c.EmpresaId == venda.EmpresaId, ct) : null;
            if (conta is null) throw new InvalidOperationException("Conta financeira do recebimento não encontrada.");
            foreach (var parcela in titulo.Parcelas)
            {
                var baixa = parcela.Baixar(venda.Data, conta.Id, parcela.Valor, 0, 0, 0, null, agora);
                db.Baixas.Add(baixa);
                db.Movimentos.Add(MovimentoFinanceiro.DaBaixa(titulo, parcela, baixa, agora));
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelarOrigemAsync(string origem, string origemId, string? usuario, CancellationToken ct = default)
    {
        var titulos = await db.Titulos.Include(t => t.Parcelas).ThenInclude(p => p.Baixas)
            .Where(t => t.Origem == origem && t.OrigemId == origemId && !t.Cancelado).ToListAsync(ct);
        var agora = relogio.GetUtcNow();
        var hoje = CalendarioBrasilia.Hoje(relogio);
        foreach (var titulo in titulos)
        {
            titulo.Reconectar();
            foreach (var parcela in titulo.Parcelas)
                foreach (var baixa in parcela.Baixas.Where(b => !b.Estornada).ToList())
                {
                    parcela.Estornar(baixa.Id, usuario, agora);
                    db.Movimentos.Add(MovimentoFinanceiro.DoEstorno(titulo, parcela, baixa, hoje, agora));
                }
            titulo.Cancelar(usuario, agora);
        }
        await db.SaveChangesAsync(ct);
    }
}
