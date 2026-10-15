using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Relatorios;

/// <summary>Divergências de um inventário (foto × contado). Parâmetros: inventarioId e zerarNaoContados.</summary>
internal sealed class RelatorioDeInventario(Lojas lojas, EstoqueDbContext db, ICatalogoDeProdutos catalogo) : IRelatorio
{
    public string Codigo => "inventario";
    public string Titulo => "Divergências do inventário";
    public IReadOnlyCollection<string> Perfis => Permissoes.Movimentar;

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var visiveis = (await lojas.VisiveisAsync(ct)).ToDictionary(l => l.Id, l => l.Nome);
        var inventario = Guid.TryParse(parametros.GetValueOrDefault("inventarioId"), out var id)
            ? await db.Inventarios.AsNoTracking().Include(i => i.Itens).SingleOrDefaultAsync(i => i.Id == id, ct)
            : null;
        if (inventario is not null && !visiveis.ContainsKey(inventario.LojaId)) inventario = null;
        var zerar = inventario?.Status == Dominio.StatusDoInventario.Concluido
            ? inventario.ZerouNaoContados
            : string.Equals(parametros.GetValueOrDefault("zerarNaoContados"), "true", StringComparison.OrdinalIgnoreCase);
        var dados = inventario is null ? null : await InventariosEndpoints.CalcularAsync(inventario, zerar, db, catalogo, verCusto: true, ct);

        var linhas = (dados?.Itens ?? []).Select(d => (IReadOnlyList<object?>)
            [d.Referencia, d.Descricao, d.Cor, d.Tamanho, d.SaldoNaAbertura, d.Contado, d.Diferenca, d.ValorDaDiferenca]).ToList();
        var filtros = inventario is null
            ? new List<FiltroAplicado> { new("Inventário", "não encontrado") }
            : [new("Loja", visiveis[inventario.LojaId]), new("Aberto em", inventario.AbertoEm.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)),
                new("Não contados", zerar ? "zerados" : "mantidos")];
        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Referência", Largura: 1f),
            new ColunaRelatorio("Descrição", Largura: 2.2f),
            new ColunaRelatorio("Cor", Largura: 0.9f),
            new ColunaRelatorio("Tamanho", Largura: 0.6f),
            new ColunaRelatorio("Saldo na abertura", TipoColuna.Inteiro, Largura: 0.8f),
            new ColunaRelatorio("Contado", TipoColuna.Inteiro, Largura: 0.7f),
            new ColunaRelatorio("Diferença", TipoColuna.Inteiro, Totalizar: true, Largura: 0.7f),
            new ColunaRelatorio("Valor da diferença", TipoColuna.Moeda, Totalizar: true, Largura: 0.9f),
        ], linhas, filtros);
    }
}
