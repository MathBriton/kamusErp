using Kamus.Infrastructure.Importacao;
using Kamus.Infrastructure.Planilhas;
using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Dominio;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Estoque.Importacao;

/// <summary>
/// Implantação do estoque: uma linha por SKU e loja com a quantidade <b>absoluta</b> (reimportar corrige, não soma).
/// O SKU vem do código de barras ou de referência + cor + tamanho. Custo vazio mantém o custo médio atual (ou o do produto).
/// </summary>
internal sealed class ImportadorDeEstoqueInicial(EstoqueDbContext db, Lojas lojas, ICatalogoDeProdutos catalogo, TimeProvider relogio) : IImportador
{
    public string Tipo => "estoque-inicial";
    public string Titulo => "Estoque inicial";
    public string Link => "/estoque/posicao";
    public IReadOnlyCollection<string> Perfis => Permissoes.Movimentar;

    public IReadOnlyList<ColunaDoModelo> Colunas =>
    [
        new("Loja", "Nome da loja. Pode ficar vazia se a empresa tiver uma loja só.", ""),
        new("Referência", "Referência do produto (ou preencha o código de barras).", "CAM-001"),
        new("Cor", "Cor do SKU, como no cadastro do produto.", "Azul"),
        new("Tamanho", "Tamanho do SKU, como na grade.", "M"),
        new("Código de barras", "Alternativa a referência + cor + tamanho.", ""),
        new("Quantidade", "Quantidade em estoque hoje (número inteiro, zero ou mais). Substitui o saldo atual.", "10", Obrigatoria: true),
        new("Custo", "Custo unitário (opcional). Vazio = custo do cadastro do produto.", "24,00"),
    ];

    private sealed record Valida(int Numero, Loja Loja, SkuDoCatalogo Sku, int Quantidade, decimal? Custo);

    private static string Chave(params string?[] partes) => string.Join('|', partes.Select(TextoParaBusca.Normalizar));

    private async Task<(List<LinhaValidada> Linhas, List<Valida> Validas)> AvaliarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct)
    {
        var linhas = new List<LinhaValidada>();
        var validas = new List<Valida>();
        if (contexto.EmpresaId is not { } empresaId)
        {
            linhas.AddRange(tabela.Linhas.Select(l => new LinhaValidada(l.Numero, AcaoDaLinha.Erro, "", ["Escolha uma empresa (não a visão de todas) para importar o estoque."])));
            return (linhas, validas);
        }

        var daEmpresa = (await lojas.DaEmpresaAsync(empresaId, contexto.EmpresaNome ?? "Loja", ct)).Where(l => l.Ativa).ToList();
        var lojaPorNome = daEmpresa.ToDictionary(l => Chave(l.Nome));
        var skus = await catalogo.TodosOsSkusAsync(ct);
        var porCodigo = skus.ToDictionary(s => s.CodigoDeBarras);
        var porReferencia = skus.ToDictionary(s => Chave(s.Referencia, s.Cor, s.Tamanho));
        var referencias = skus.Select(s => Chave(s.Referencia)).ToHashSet();
        var lojaIds = daEmpresa.Select(l => l.Id).ToList();
        var existentes = (await db.Saldos.Where(s => lojaIds.Contains(s.LojaId)).Select(s => new { s.LojaId, s.SkuId, s.Quantidade }).ToListAsync(ct))
            .ToDictionary(s => (s.LojaId, s.SkuId), s => s.Quantidade);
        var vistos = new HashSet<(Guid, Guid)>();

        foreach (var l in tabela.Linhas)
        {
            var erros = new List<string>();
            var nomeDaLoja = l.Valor("loja");
            Loja? loja = null;
            if (nomeDaLoja is null)
            {
                if (daEmpresa.Count == 1) loja = daEmpresa[0];
                else erros.Add("Informe a loja (a empresa tem mais de uma).");
            }
            else if (!lojaPorNome.TryGetValue(Chave(nomeDaLoja), out loja)) erros.Add($"Loja {nomeDaLoja} não encontrada (ou desativada).");

            SkuDoCatalogo? sku = null;
            var (codigo, referencia, cor, tamanho) = (l.Valor("codigodebarras", "ean", "codigo"), l.Valor("referencia", "ref"), l.Valor("cor"), l.Valor("tamanho", "tam"));
            if (codigo is not null)
            {
                if (!porCodigo.TryGetValue(codigo, out sku)) erros.Add($"Código de barras {codigo} não encontrado.");
            }
            else if (referencia is null || cor is null || tamanho is null) erros.Add("Informe o código de barras ou referência, cor e tamanho.");
            else if (!referencias.Contains(Chave(referencia))) erros.Add($"Produto {referencia} não encontrado.");
            else if (!porReferencia.TryGetValue(Chave(referencia, cor, tamanho), out sku)) erros.Add($"O produto {referencia} não tem a combinação {cor} {tamanho}.");

            var (quantidade, quantidadeOk) = ValoresDaPlanilha.Numero(l.Valor("quantidade", "qtd", "saldo"));
            if (quantidade is null) erros.Add(quantidadeOk ? "Informe a quantidade." : "Quantidade inválida.");
            else if (quantidade < 0 || quantidade % 1 != 0) erros.Add("A quantidade precisa ser um número inteiro, zero ou mais.");
            var (custo, custoOk) = ValoresDaPlanilha.Numero(l.Valor("custo", "custounitario"));
            if (!custoOk || custo < 0) erros.Add("Custo inválido.");

            if (loja is not null && sku is not null && !vistos.Add((loja.Id, sku.Id)))
                erros.Add($"{Saldos.Nome(sku)} repetido para {loja.Nome}: deixe uma linha só.");

            var descricao = sku is null ? referencia ?? codigo ?? "" : $"{Saldos.Nome(sku)}: {quantidade:0} peça(s){(loja is null ? "" : $" em {loja.Nome}")}";
            if (erros.Count > 0)
            {
                linhas.Add(new LinhaValidada(l.Numero, AcaoDaLinha.Erro, descricao, erros));
                continue;
            }
            var atual = existentes.TryGetValue((loja!.Id, sku!.Id), out var q) ? q : (int?)null;
            linhas.Add(new LinhaValidada(l.Numero, atual is null ? AcaoDaLinha.Criar : AcaoDaLinha.Atualizar,
                atual is null ? descricao : $"{descricao} (hoje {atual})", []));
            validas.Add(new Valida(l.Numero, loja, sku, (int)quantidade!.Value, custo));
        }
        return (linhas, validas);
    }

    public async Task<IReadOnlyList<LinhaValidada>> ValidarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct) =>
        (await AvaliarAsync(tabela, contexto, ct)).Linhas;

    public async Task<ResultadoDaImportacao> ImportarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct)
    {
        var (linhas, validas) = await AvaliarAsync(tabela, contexto, ct);
        var op = new Operacao(Guid.NewGuid(), relogio.GetUtcNow(), contexto.UsuarioId, contexto.UsuarioNome, "Estoque inicial", "Planilha", null, null);
        var (criados, atualizados) = (0, 0);
        foreach (var porLoja in validas.GroupBy(v => v.Loja.Id))
        {
            var saldos = await Saldos.DaLojaAsync(db, porLoja.Key, porLoja.Select(v => v.Sku), ct);
            foreach (var v in porLoja)
            {
                var saldo = saldos[v.Sku.Id];
                var novo = db.Entry(saldo).State == EntityState.Added;
                var custo = v.Custo ?? (novo || saldo.Quantidade <= 0 ? v.Sku.PrecoCusto : null);
                if (saldo.DefinirInicial(v.Quantidade, custo, op) is { } movimento) db.Movimentos.Add(movimento);
                if (novo) criados++;
                else atualizados++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDaImportacao(criados, atualizados, linhas.Count(l => l.Acao == AcaoDaLinha.Erro));
    }
}
