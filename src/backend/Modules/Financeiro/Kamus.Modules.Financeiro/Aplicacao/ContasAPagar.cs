using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <summary>Registra a conta a pagar de uma compra (uma parcela por duplicata), na categoria "Compra de mercadorias" (criada se faltar).</summary>
internal sealed class ContasAPagar(FinanceiroDbContext db, TimeProvider relogio) : IContasAPagar
{
    public const string CategoriaDeCompras = "Compra de mercadorias";

    public async Task<Guid> RegistrarCompraAsync(ContaAPagarDeCompra conta, CancellationToken ct = default)
    {
        var existente = await db.Titulos.Where(t => t.Origem == conta.Origem && t.OrigemId == conta.OrigemId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return existente.Value;

        var chave = TextoParaBusca.Normalizar(CategoriaDeCompras);
        var categoria = (await db.Categorias.Where(c => c.Tipo == TipoDeCategoria.Despesa).ToListAsync(ct)).FirstOrDefault(c => TextoParaBusca.Normalizar(c.Nome) == chave);
        if (categoria is null)
        {
            categoria = Categoria.Criar(CategoriaDeCompras, "Mercadorias", TipoDeCategoria.Despesa);
            db.Categorias.Add(categoria);
        }

        var titulo = Titulo.Criar(conta.EmpresaId,
            new DadosDoTitulo(TipoDeTitulo.Pagar, conta.Descricao, conta.FornecedorId, conta.FornecedorNome, categoria.Id, conta.Documento, conta.Competencia,
                "Gerado automaticamente pelo lançamento da nota no estoque."),
            conta.Parcelas.Select(p => new NovaParcela(p.Vencimento, p.Valor)).ToList(), relogio.GetUtcNow(), conta.Origem, conta.OrigemId);
        db.Titulos.Add(titulo);
        await db.SaveChangesAsync(ct);
        return titulo.Id;
    }
}
