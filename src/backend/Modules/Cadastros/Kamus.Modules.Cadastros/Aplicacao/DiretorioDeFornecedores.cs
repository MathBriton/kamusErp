using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Aplicacao;

internal sealed class DiretorioDeFornecedores(CadastrosDbContext db, TimeProvider relogio) : IDiretorioDeFornecedores
{
    private static (TipoPessoa Tipo, string? Documento) Normalizar(string documento)
    {
        var digitos = new string(documento.Where(char.IsAsciiLetterOrDigit).ToArray());
        var tipo = digitos.Length == 11 ? TipoPessoa.Fisica : TipoPessoa.Juridica;
        return (tipo, Pessoa.NormalizarDocumento(tipo, digitos));
    }

    private static FornecedorResumo Resumo(Pessoa p) => new(p.Id, p.Nome, p.Documento!);

    public async Task<FornecedorResumo?> PorDocumentoAsync(string documento, CancellationToken ct = default)
    {
        var (_, normalizado) = Normalizar(documento);
        if (normalizado is null) return null;
        var pessoa = await db.Pessoas.AsNoTracking().FirstOrDefaultAsync(p => p.Documento == normalizado, ct);
        return pessoa is null ? null : Resumo(pessoa);
    }

    public async Task<FornecedorResumo> GarantirAsync(NovoFornecedor dados, CancellationToken ct = default)
    {
        var (tipo, documento) = Normalizar(dados.Documento);
        if (documento is null) throw new ArgumentException($"Documento do fornecedor inválido: {dados.Documento}.", nameof(dados));
        var agora = relogio.GetUtcNow();
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var pessoa = await db.Pessoas.FirstOrDefaultAsync(p => p.Documento == documento, ct);
        if (pessoa is not null)
        {
            if (!pessoa.EhFornecedor)
            {
                pessoa.Atualizar(DadosAtuais(pessoa) with { EhFornecedor = true }, agora, hoje);
                await db.SaveChangesAsync(ct);
            }
            return Resumo(pessoa);
        }

        var cep = dados.Cep is { Length: 8 } c ? $"{c[..5]}-{c[5..]}" : dados.Cep;
        pessoa = Pessoa.Criar(new DadosDaPessoa
        {
            Tipo = tipo,
            Nome = dados.RazaoSocial,
            NomeFantasia = dados.NomeFantasia,
            Documento = documento,
            EhFornecedor = true,
            Telefone = Telefone.Normalizar(dados.Telefone) is null ? null : dados.Telefone,
            Endereco = new Endereco { Cep = cep, Logradouro = dados.Logradouro, Numero = dados.Numero, Bairro = dados.Bairro, Cidade = dados.Cidade, Uf = dados.Uf },
            Observacoes = "Cadastrado automaticamente pela importação do XML da NF-e.",
        }, agora, hoje);
        db.Pessoas.Add(pessoa);
        await db.SaveChangesAsync(ct);
        return Resumo(pessoa);
    }

    private static DadosDaPessoa DadosAtuais(Pessoa p) => new()
    {
        Tipo = p.Tipo,
        Nome = p.Nome,
        NomeFantasia = p.NomeFantasia,
        Documento = p.Documento,
        EhCliente = p.EhCliente,
        EhFornecedor = p.EhFornecedor,
        Telefone = p.Telefone,
        Email = p.Email,
        DataNascimento = p.DataNascimento,
        Endereco = p.Endereco,
        Preferencias = p.Preferencias,
        Observacoes = p.Observacoes,
        AceitaMarketing = p.ConsentimentoMarketingEm is not null,
    };
}
