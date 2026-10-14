using Kamus.Infrastructure.Importacao;
using Kamus.Infrastructure.Planilhas;
using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Importacao;

/// <summary>
/// Clientes ou fornecedores. Casa com o cadastro existente pelo CPF/CNPJ, senão pelo telefone, senão pelo nome — então
/// reimportar a mesma planilha atualiza em vez de duplicar. Células vazias mantêm o que já existe.
/// </summary>
internal abstract class ImportadorDePessoas(CadastrosDbContext db, TimeProvider relogio) : IImportador
{
    protected abstract bool Cliente { get; }
    public abstract string Tipo { get; }
    public abstract string Titulo { get; }
    public string Link => $"/cadastros/{Tipo}";
    public IReadOnlyCollection<string> Perfis => Permissoes.Gestao;

    public IReadOnlyList<ColunaDoModelo> Colunas =>
    [
        new("Tipo", "Física ou Jurídica. Se vazio, vem do documento (11 dígitos = CPF, 14 = CNPJ).", Cliente ? "Física" : "Jurídica"),
        new(Cliente ? "Nome" : "Razão social", "Nome completo ou razão social.", Cliente ? "Maria Silva" : "Malharia Sul LTDA", Obrigatoria: true),
        new("Nome fantasia", "Só para pessoa jurídica.", Cliente ? "" : "Malharia Sul"),
        new("CPF/CNPJ", "Opcional, com ou sem pontuação. Identifica o cadastro na reimportação.", Cliente ? "529.982.247-25" : "11.222.333/0001-81"),
        new("Telefone", "Com DDD. Ex.: (11) 98765-4321.", "(13) 99876-5432"),
        new("E-mail", "", Cliente ? "maria@email.com" : "vendas@malhariasul.com.br"),
        new("Nascimento", "dd/mm/aaaa.", Cliente ? "14/10/1990" : ""),
        new("CEP", "", "11010-000"),
        new("Logradouro", "", "Rua XV de Novembro"),
        new("Número", "", "100"),
        new("Complemento", "", ""),
        new("Bairro", "", "Centro"),
        new("Cidade", "", "Santos"),
        new("UF", "Sigla com 2 letras.", "SP"),
        new("Observações", "", ""),
        .. Cliente
            ? new ColunaDoModelo[]
            {
                new("Blusa", "Tamanho preferido.", "M"),
                new("Calça", "Tamanho preferido.", "40"),
                new("Calçado", "Tamanho preferido.", "37"),
                new("Aceita marketing", "Sim ou Não (LGPD: só marque Sim com o consentimento do cliente).", "Sim"),
            }
            : [],
    ];

    private sealed record Avaliada(LinhaValidada Linha, DadosDaPessoa? Dados, Pessoa? Existente);

    private async Task<List<Avaliada>> AvaliarAsync(TabelaLida tabela, CancellationToken ct)
    {
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var existentes = await db.Pessoas.ToListAsync(ct);
        var documentosNaPlanilha = new Dictionary<string, int>();
        var resultado = new List<Avaliada>();

        foreach (var l in tabela.Linhas)
        {
            var erros = new List<string>();
            var documento = l.Valor("cpfcnpj", "cpf", "cnpj", "documento");
            var digitos = documento is null ? 0 : documento.Count(char.IsLetterOrDigit);
            TipoPessoa? tipoInformado = TextoParaBusca.Normalizar(l.Valor("tipo")) switch
            {
                "fisica" or "f" or "pf" or "pessoa fisica" => TipoPessoa.Fisica,
                "juridica" or "j" or "pj" or "pessoa juridica" => TipoPessoa.Juridica,
                _ => digitos == 14 ? TipoPessoa.Juridica : digitos == 11 ? TipoPessoa.Fisica : null,
            };
            var (nascimento, nascimentoOk) = ValoresDaPlanilha.Data(l.Valor("nascimento", "datadenascimento"));
            if (!nascimentoOk) erros.Add("Data de nascimento inválida (use dd/mm/aaaa).");

            var normalizado = documento is null ? null
                : Pessoa.NormalizarDocumento(tipoInformado ?? TipoPessoa.Fisica, documento) ?? Pessoa.NormalizarDocumento(TipoPessoa.Juridica, documento);
            if (normalizado is not null)
            {
                if (documentosNaPlanilha.TryGetValue(normalizado, out var anterior)) erros.Add($"CPF/CNPJ repetido na planilha (linha {anterior}).");
                else documentosNaPlanilha[normalizado] = l.Numero;
            }

            var telefone = Telefone.Normalizar(l.Valor("telefone", "telefonewhatsapp", "whatsapp", "celular"));
            var nome = l.Valor("nome", "nomerazaosocial", "razaosocial");
            var existente = (normalizado is null ? null : existentes.FirstOrDefault(p => p.Documento == normalizado))
                ?? (telefone is null ? null : existentes.FirstOrDefault(p => p.Telefone == telefone))
                ?? (nome is null || normalizado is not null ? null : existentes.FirstOrDefault(p => TextoParaBusca.Normalizar(p.Nome) == TextoParaBusca.Normalizar(nome)));
            var tipo = tipoInformado ?? existente?.Tipo ?? (Cliente ? TipoPessoa.Fisica : TipoPessoa.Juridica);
            if (existente?.AnonimizadoEm is not null) erros.Add("O cadastro correspondente foi anonimizado (LGPD) e não pode ser alterado.");

            var e = existente?.Endereco ?? new Endereco();
            var p = existente?.Preferencias ?? new PreferenciasDeModa();
            var dados = new DadosDaPessoa
            {
                Tipo = tipo,
                Nome = nome ?? existente?.Nome,
                NomeFantasia = l.Valor("nomefantasia", "fantasia") ?? existente?.NomeFantasia,
                Documento = documento ?? existente?.Documento,
                EhCliente = Cliente || existente?.EhCliente == true,
                EhFornecedor = !Cliente || existente?.EhFornecedor == true,
                Telefone = l.Valor("telefone", "telefonewhatsapp", "whatsapp", "celular") ?? existente?.Telefone,
                Email = l.Valor("email") ?? existente?.Email,
                DataNascimento = nascimento ?? existente?.DataNascimento,
                Endereco = new Endereco
                {
                    Cep = l.Valor("cep") ?? e.Cep,
                    Logradouro = l.Valor("logradouro", "endereco", "rua") ?? e.Logradouro,
                    Numero = l.Valor("numero") ?? e.Numero,
                    Complemento = l.Valor("complemento") ?? e.Complemento,
                    Bairro = l.Valor("bairro") ?? e.Bairro,
                    Cidade = l.Valor("cidade") ?? e.Cidade,
                    Uf = l.Valor("uf", "estado") ?? e.Uf,
                },
                Preferencias = new PreferenciasDeModa
                {
                    Blusa = l.Valor("blusa") ?? p.Blusa,
                    Calca = l.Valor("calca") ?? p.Calca,
                    Calcado = l.Valor("calcado") ?? p.Calcado,
                    Estilo = l.Valor("estilo") ?? p.Estilo,
                },
                Observacoes = l.Valor("observacoes", "obs") ?? existente?.Observacoes,
                AceitaMarketing = ValoresDaPlanilha.SimNao(l.Valor("aceitamarketing", "aceitamensagens")) ?? existente?.ConsentimentoMarketingEm is not null,
            };
            erros.AddRange(Pessoa.Validar(dados, hoje));

            var papel = Cliente ? "cliente" : "fornecedor";
            var descricao = $"{dados.Nome ?? "(sem nome)"} — {(existente is null ? $"{papel} novo" : "atualiza o cadastro existente")}";
            var acao = erros.Count > 0 ? AcaoDaLinha.Erro : existente is null ? AcaoDaLinha.Criar : AcaoDaLinha.Atualizar;
            resultado.Add(new Avaliada(new LinhaValidada(l.Numero, acao, descricao, erros), erros.Count == 0 ? dados : null, existente));
        }
        return resultado;
    }

    public async Task<IReadOnlyList<LinhaValidada>> ValidarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct) =>
        (await AvaliarAsync(tabela, ct)).Select(a => a.Linha).ToList();

    public async Task<ResultadoDaImportacao> ImportarAsync(TabelaLida tabela, ContextoDaImportacao contexto, CancellationToken ct)
    {
        var avaliadas = await AvaliarAsync(tabela, ct);
        var agora = relogio.GetUtcNow();
        var hoje = CalendarioBrasilia.Hoje(relogio);
        int criados = 0, atualizados = 0;
        foreach (var a in avaliadas.Where(a => a.Dados is not null))
        {
            if (a.Existente is { } existente)
            {
                existente.Atualizar(a.Dados!, agora, hoje);
                atualizados++;
            }
            else
            {
                db.Pessoas.Add(Pessoa.Criar(a.Dados!, agora, hoje));
                criados++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDaImportacao(criados, atualizados, avaliadas.Count(a => a.Dados is null));
    }
}

internal sealed class ImportadorDeClientes(CadastrosDbContext db, TimeProvider relogio) : ImportadorDePessoas(db, relogio)
{
    protected override bool Cliente => true;
    public override string Tipo => "clientes";
    public override string Titulo => "Clientes";
}

internal sealed class ImportadorDeFornecedores(CadastrosDbContext db, TimeProvider relogio) : ImportadorDePessoas(db, relogio)
{
    protected override bool Cliente => false;
    public override string Tipo => "fornecedores";
    public override string Titulo => "Fornecedores";
}
