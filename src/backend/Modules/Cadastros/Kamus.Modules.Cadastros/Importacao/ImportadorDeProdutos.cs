using Kamus.Infrastructure.Importacao;
using Kamus.Infrastructure.Planilhas;
using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Importacao;

/// <summary>
/// Uma linha por SKU (referência + cor + tamanho). Linhas da mesma referência formam um produto: os dados do produto vêm
/// da primeira linha que os informar. Cores que não existem são criadas. Produto novo: combinações fora da planilha ficam
/// desativadas. Produto existente: as combinações da planilha são ativadas/atualizadas e as outras não mudam.
/// </summary>
internal sealed class ImportadorDeProdutos(CadastrosDbContext db, TimeProvider relogio) : IImportador
{
    public string Tipo => "produtos";
    public string Titulo => "Produtos";
    public string Link => "/cadastros/produtos";
    public IReadOnlyCollection<string> Perfis => Permissoes.Catalogo;

    public IReadOnlyList<ColunaDoModelo> Colunas =>
    [
        new("Referência", "Código do produto (modelo). Repita em todas as linhas do mesmo produto.", "CAM-001", Obrigatoria: true),
        new("Descrição", "Obrigatória para produto novo (basta na primeira linha).", "Camiseta Básica Gola V"),
        new("Marca", "", "Kamus"),
        new("Categoria", "", "Camisetas"),
        new("Coleção", "", "Verão 2027"),
        new("Ref. do fornecedor", "", "MS-4521"),
        new("Grade", "Nome de uma grade cadastrada (Cadastros → Grades e cores). Ex.: PP ao GG.", "PP ao GG", Obrigatoria: true),
        new("Cor", "Cores que não existirem serão criadas.", "Azul", Obrigatoria: true),
        new("Tamanho", "Precisa existir na grade.", "M", Obrigatoria: true),
        new("Código de barras", "EAN do fornecedor (opcional). Vazio = código interno gerado pelo sistema.", ""),
        new("Preço de venda", "Obrigatório para produto novo. Ex.: 59,90.", "59,90"),
        new("Custo", "Opcional. Ex.: 24,00.", "24,00"),
        new("Preço do SKU", "Opcional: preço diferente só para esta cor/tamanho.", ""),
    ];

    private sealed record Linha(int Numero, string Referencia, string Cor, string Tamanho, string? Codigo, decimal? PrecoSku);

    private sealed record Grupo(string Referencia, Produto? Existente, GradeDeTamanhos? Grade, DadosDoProduto? Dados, List<Linha> Validas);

    private sealed record Avaliacao(List<LinhaValidada> Linhas, List<Grupo> Grupos);

    private static string Chave(string? texto) => TextoParaBusca.Normalizar(texto);

    private async Task<Avaliacao> AvaliarAsync(TabelaLida tabela, CancellationToken ct)
    {
        var grades = await db.Grades.ToListAsync(ct);
        var cores = await db.Cores.ToListAsync(ct);
        var produtos = await db.Produtos.Include(p => p.Skus).ToListAsync(ct);
        var codigosExistentes = produtos.SelectMany(p => p.Skus.Select(s => (s.CodigoDeBarras, p.Referencia, s.CorId, s.Tamanho)))
            .ToDictionary(x => x.CodigoDeBarras, x => x);

        var linhas = new List<LinhaValidada>();
        var grupos = new List<Grupo>();
        var codigosNaPlanilha = new Dictionary<string, int>();

        foreach (var porReferencia in tabela.Linhas.GroupBy(l => (l.Valor("referencia", "ref", "codigo") ?? "").Trim().ToUpperInvariant()))
        {
            var referencia = porReferencia.Key;
            var existente = produtos.FirstOrDefault(p => p.Referencia == referencia);
            var gradeDoProduto = existente is null ? null : grades.Single(g => g.Id == existente.GradeId);
            var nomeDaGrade = porReferencia.Select(l => l.Valor("grade")).FirstOrDefault(v => v is not null);
            var grade = gradeDoProduto ?? grades.FirstOrDefault(g => Chave(g.Nome) == Chave(nomeDaGrade));
            string? Primeiro(params string[] colunas) => porReferencia.Select(l => l.Valor(colunas)).FirstOrDefault(v => v is not null);
            var (preco, precoOk) = ValoresDaPlanilha.Numero(Primeiro("precodevenda", "preco"));
            var (custo, custoOk) = ValoresDaPlanilha.Numero(Primeiro("custo", "precodecusto"));

            var dados = new DadosDoProduto
            {
                Referencia = referencia,
                Descricao = Primeiro("descricao") ?? existente?.Descricao,
                Marca = Primeiro("marca") ?? existente?.Marca,
                Categoria = Primeiro("categoria") ?? existente?.Categoria,
                Colecao = Primeiro("colecao") ?? existente?.Colecao,
                ReferenciaDoFornecedor = Primeiro("referenciadofornecedor", "refdofornecedor", "reffornecedor") ?? existente?.ReferenciaDoFornecedor,
                FornecedorId = existente?.FornecedorId,
                PrecoVenda = preco ?? existente?.PrecoVenda ?? -1,
                PrecoCusto = custo ?? existente?.PrecoCusto,
            };

            // Erros que valem para o produto inteiro (aparecem em todas as linhas dele).
            var errosDoProduto = new List<string>();
            if (!precoOk) errosDoProduto.Add("Preço de venda inválido.");
            if (!custoOk) errosDoProduto.Add("Custo inválido.");
            if (existente is null && preco is null && precoOk) errosDoProduto.Add("Informe o preço de venda (produto novo).");
            if (grade is null) errosDoProduto.Add(nomeDaGrade is null ? "Informe a grade." : $"Grade \"{nomeDaGrade}\" não encontrada. Cadastre em Grades e cores.");
            else if (gradeDoProduto is not null && nomeDaGrade is not null && Chave(nomeDaGrade) != Chave(gradeDoProduto.Nome))
                errosDoProduto.Add($"O produto {referencia} já usa a grade \"{gradeDoProduto.Nome}\"; a grade não muda.");
            errosDoProduto.AddRange(Produto.Validar(dados with { PrecoVenda = Math.Max(dados.PrecoVenda, 0) }));

            var validas = new List<Linha>();
            var combinacoes = new HashSet<(string, string)>();
            foreach (var l in porReferencia)
            {
                var erros = new List<string>(errosDoProduto);
                var cor = l.Valor("cor");
                var tamanhoInformado = l.Valor("tamanho", "tam");
                if (cor is null) erros.Add("Informe a cor.");
                if (tamanhoInformado is null) erros.Add("Informe o tamanho.");
                var tamanho = grade?.Tamanhos.FirstOrDefault(t => string.Equals(t, tamanhoInformado, StringComparison.OrdinalIgnoreCase));
                if (grade is not null && tamanhoInformado is not null && tamanho is null)
                    erros.Add($"Tamanho \"{tamanhoInformado}\" não existe na grade \"{grade.Nome}\".");
                if (cor is not null && tamanho is not null && !combinacoes.Add((Chave(cor), tamanho)))
                    erros.Add($"{cor} {tamanho} aparece mais de uma vez para {referencia}.");

                var codigoInformado = l.Valor("codigodebarras", "ean", "gtin", "codigo de barras");
                var codigo = CodigoDeBarras.Normalizar(codigoInformado);
                if (codigoInformado is not null && !CodigoDeBarras.Valido(codigoInformado)) erros.Add("Código de barras inválido (confira o dígito verificador).");
                else if (codigo is not null)
                {
                    var corExistente = cores.FirstOrDefault(c => Chave(c.Nome) == Chave(cor));
                    if (codigosNaPlanilha.TryGetValue(codigo, out var anterior)) erros.Add($"Código de barras repetido na planilha (linha {anterior}).");
                    else if (codigosExistentes.TryGetValue(codigo, out var dono) && !(dono.Referencia == referencia && dono.CorId == corExistente?.Id && dono.Tamanho == tamanho))
                        erros.Add($"Código de barras já usado no produto {dono.Referencia}.");
                    codigosNaPlanilha.TryAdd(codigo, l.Numero);
                }
                var (precoSku, precoSkuOk) = ValoresDaPlanilha.Numero(l.Valor("precodosku"));
                if (!precoSkuOk || precoSku < 0) erros.Add("Preço do SKU inválido.");

                var corNova = cor is not null && cores.All(c => Chave(c.Nome) != Chave(cor));
                var descricao = $"{(referencia.Length > 0 ? referencia : "(sem referência)")} · {cor ?? "?"} · {tamanhoInformado ?? "?"}"
                    + (existente is null ? " — produto novo" : " — atualiza o produto") + (corNova ? $", cor nova: {cor}" : "");
                var acao = erros.Count > 0 ? AcaoDaLinha.Erro : existente is null ? AcaoDaLinha.Criar : AcaoDaLinha.Atualizar;
                linhas.Add(new LinhaValidada(l.Numero, acao, descricao, erros.Distinct().ToList()));
                if (erros.Count == 0) validas.Add(new Linha(l.Numero, referencia, cor!.Trim(), tamanho!, codigo, precoSku));
            }
            grupos.Add(new Grupo(referencia, existente, grade, errosDoProduto.Count == 0 ? dados : null, validas));
        }
        return new Avaliacao(linhas.OrderBy(l => l.Numero).ToList(), grupos);
    }

    public async Task<IReadOnlyList<LinhaValidada>> ValidarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct) => (await AvaliarAsync(tabela, ct)).Linhas;

    public async Task<ResultadoDaImportacao> ImportarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct)
    {
        var avaliacao = await AvaliarAsync(tabela, ct);
        var cores = await db.Cores.ToListAsync(ct);
        var gerar = await ProdutosEndpoints.GeradorDeCodigosAsync(db, ct);
        var agora = relogio.GetUtcNow();
        int criados = 0, atualizados = 0;

        Guid CorId(string nome)
        {
            var cor = cores.FirstOrDefault(c => Chave(c.Nome) == Chave(nome));
            if (cor is null)
            {
                cor = Cor.Criar(nome, null);
                cores.Add(cor);
                db.Cores.Add(cor);
            }
            return cor.Id;
        }

        foreach (var grupo in avaliacao.Grupos.Where(g => g.Validas.Count > 0 && g.Dados is not null))
        {
            var linhas = grupo.Validas.Select(l => (l, CorId: CorId(l.Cor))).ToList();
            var coresDaPlanilha = linhas.Select(x => x.CorId).Distinct().ToList();
            Produto produto;
            HashSet<Guid> coresNovas;
            if (grupo.Existente is { } existente)
            {
                produto = existente;
                var ativas = produto.Skus.Where(s => s.Ativo).Select(s => s.CorId).Distinct().ToList();
                var antes = produto.Skus.Select(s => s.Id).ToHashSet();
                coresNovas = coresDaPlanilha.Where(c => produto.Skus.All(s => s.CorId != c)).ToHashSet();
                produto.Atualizar(grupo.Dados!);
                produto.DefinirCores(ativas.Union(coresDaPlanilha).ToList(), grupo.Grade!.Tamanhos, gerar);
                foreach (var novo in produto.Skus.Where(s => !antes.Contains(s.Id))) db.Skus.Add(novo);
                atualizados++;
            }
            else
            {
                produto = Produto.Criar(grupo.Dados!, grupo.Grade!.Id, grupo.Grade.Tamanhos, coresDaPlanilha, gerar, agora);
                db.Produtos.Add(produto);
                coresNovas = coresDaPlanilha.ToHashSet();
                criados++;
            }

            // Combinações de cores novas que não estão na planilha: não existem (desativadas).
            var naPlanilha = linhas.Select(x => (x.CorId, x.l.Tamanho)).ToHashSet();
            foreach (var sku in produto.Skus.Where(s => coresNovas.Contains(s.CorId) && !naPlanilha.Contains((s.CorId, s.Tamanho))))
                produto.AlterarSku(sku.Id, sku.CodigoDeBarras, sku.PrecoVenda, ativo: false);
            foreach (var (l, corId) in linhas)
            {
                var sku = produto.Skus.Single(s => s.CorId == corId && s.Tamanho == l.Tamanho);
                produto.AlterarSku(sku.Id, l.Codigo ?? sku.CodigoDeBarras, l.PrecoSku ?? sku.PrecoVenda, ativo: true);
            }
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDaImportacao(criados, atualizados, avaliacao.Linhas.Count(l => l.Acao == AcaoDaLinha.Erro));
    }
}
