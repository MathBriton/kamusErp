using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Relatorios;

namespace Kamus.Modules.Cadastros.Relatorios;

/// <summary>Lista de clientes ou de fornecedores, com os mesmos filtros da tela (busca, situacao).</summary>
internal abstract class RelatorioDePessoas(CadastrosDbContext db, IEmpresaAtiva empresa, string papel, string singular) : IRelatorio
{
    public abstract string Codigo { get; }
    public abstract string Titulo { get; }
    public abstract IReadOnlyCollection<string> Perfis { get; }
    public string Area => "Cadastros";
    public string? Descricao => $"Lista de {Titulo.ToLowerInvariant()} com documento, contato e cidade.";

    public async Task<DadosDoRelatorio> GerarAsync(IReadOnlyDictionary<string, string?> parametros, CancellationToken ct)
    {
        var busca = parametros.GetValueOrDefault("busca");
        var situacao = parametros.GetValueOrDefault("situacao");
        // "ids" = linhas selecionadas na grade: exporta só elas, ignorando os demais filtros.
        var ids = ParametrosDoRelatorio.Ids(parametros);
        var pessoas = ids.Count > 0
            ? [.. (await PessoasEndpoints.FiltrarAsync(db, empresa.Perfil, papel, "todos", null, ct)).Where(p => ids.Contains(p.Id))]
            : await PessoasEndpoints.FiltrarAsync(db, empresa.Perfil, papel, situacao, busca, ct);

        var linhas = pessoas.Select(p => (IReadOnlyList<object?>)
        [
            p.Nome, p.NomeFantasia, p.DocumentoFormatado, Dominio.Telefone.Formatar(p.Telefone), p.Email,
            p.Endereco.Cidade is null ? null : $"{p.Endereco.Cidade}/{p.Endereco.Uf}",
            p.DataNascimento, p.ConsentimentoMarketingEm is null ? "Não" : "Sim", p.Ativo ? "Ativo" : "Inativo",
        ]).ToList();

        var filtros = new List<FiltroAplicado>();
        if (ids.Count > 0) filtros.Add(ParametrosDoRelatorio.Selecionados(pessoas.Count, singular, Titulo.ToLowerInvariant()));
        else
        {
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add(new FiltroAplicado("Busca", busca));
            filtros.Add(new FiltroAplicado("Situação", situacao switch { "inativos" => "Inativos", "todos" => "Todos", _ => "Ativos" }));
        }

        return new DadosDoRelatorio(Titulo,
        [
            new ColunaRelatorio("Nome", Largura: 2.2f),
            new ColunaRelatorio("Nome fantasia", Largura: 1.6f),
            new ColunaRelatorio("CPF/CNPJ", Largura: 1.4f),
            new ColunaRelatorio("Telefone", Largura: 1.1f),
            new ColunaRelatorio("E-mail", Largura: 1.8f),
            new ColunaRelatorio("Cidade/UF", Largura: 1.3f),
            new ColunaRelatorio("Nascimento", TipoColuna.Data, Largura: 1f),
            new ColunaRelatorio("Aceita marketing", Largura: 0.9f),
            new ColunaRelatorio("Situação", Largura: 0.8f),
        ], linhas, filtros);
    }
}

internal sealed class RelatorioDeClientes(CadastrosDbContext db, IEmpresaAtiva empresa) : RelatorioDePessoas(db, empresa, "cliente", "cliente")
{
    public override string Codigo => "clientes";
    public override string Titulo => "Clientes";
    public override IReadOnlyCollection<string> Perfis => Permissoes.Clientes;
}

internal sealed class RelatorioDeFornecedores(CadastrosDbContext db, IEmpresaAtiva empresa) : RelatorioDePessoas(db, empresa, "fornecedor", "fornecedor")
{
    public override string Codigo => "fornecedores";
    public override string Titulo => "Fornecedores";
    public override IReadOnlyCollection<string> Perfis => Permissoes.Fornecedores;
}
