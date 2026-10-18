using Kamus.Modules.Financeiro.Dominio;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Financeiro.Aplicacao;

/// <summary>Contas a pagar de outros módulos: compras (uma parcela por duplicata, categoria "Compra de mercadorias") e contas avulsas (ex.: comissões).</summary>
internal sealed class ContasAPagar(FinanceiroDbContext db, TimeProvider relogio) : IContasAPagar
{
    public const string CategoriaDeCompras = "Compra de mercadorias";

    public async Task<Guid> RegistrarCompraAsync(ContaAPagarDeCompra conta, CancellationToken ct = default)
    {
        var existente = await db.Titulos.Where(t => t.Origem == conta.Origem && t.OrigemId == conta.OrigemId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return existente.Value;

        var categoria = await CategoriaAsync("Mercadorias", CategoriaDeCompras, ct);

        var titulo = Titulo.Criar(conta.EmpresaId,
            new DadosDoTitulo(TipoDeTitulo.Pagar, conta.Descricao, conta.FornecedorId, conta.FornecedorNome, categoria.Id, conta.Documento, conta.Competencia,
                "Gerado automaticamente pelo lançamento da nota no estoque."),
            conta.Parcelas.Select(p => new NovaParcela(p.Vencimento, p.Valor)).ToList(), relogio.GetUtcNow(), conta.Origem, conta.OrigemId);
        db.Titulos.Add(titulo);
        await db.SaveChangesAsync(ct);
        return titulo.Id;
    }

    public async Task<Guid> RegistrarContaAsync(NovaContaAPagar conta, CancellationToken ct = default)
    {
        var existente = await db.Titulos.Where(t => t.Origem == conta.Origem && t.OrigemId == conta.OrigemId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return existente.Value;

        var categoria = await CategoriaAsync(conta.GrupoDaCategoria, conta.Categoria, ct);
        var titulo = Titulo.Criar(conta.EmpresaId,
            new DadosDoTitulo(TipoDeTitulo.Pagar, conta.Descricao, null, conta.PessoaNome, categoria.Id, null, conta.Vencimento, "Gerado automaticamente."),
            [new NovaParcela(conta.Vencimento, conta.Valor)], relogio.GetUtcNow(), conta.Origem, conta.OrigemId);
        db.Titulos.Add(titulo);
        await db.SaveChangesAsync(ct);
        return titulo.Id;
    }

    public async Task<bool> CancelarOrigemAsync(string origem, string origemId, string? usuario, CancellationToken ct = default)
    {
        var titulos = await db.Titulos.Include(t => t.Parcelas).ThenInclude(p => p.Baixas)
            .Where(t => t.Origem == origem && t.OrigemId == origemId && !t.Cancelado).ToListAsync(ct);
        if (titulos.Any(t => t.Parcelas.Any(p => p.Baixas.Any(b => !b.Estornada)))) return false;
        foreach (var titulo in titulos)
        {
            titulo.Reconectar();
            titulo.Cancelar(usuario, relogio.GetUtcNow());
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Categoria> CategoriaAsync(string grupo, string nome, CancellationToken ct)
    {
        var chave = TextoParaBusca.Normalizar(nome);
        var categoria = (await db.Categorias.Where(c => c.Tipo == TipoDeCategoria.Despesa).ToListAsync(ct)).FirstOrDefault(c => TextoParaBusca.Normalizar(c.Nome) == chave);
        if (categoria is not null) return categoria;
        categoria = Categoria.Criar(nome, grupo, TipoDeCategoria.Despesa);
        db.Categorias.Add(categoria);
        return categoria;
    }
}
