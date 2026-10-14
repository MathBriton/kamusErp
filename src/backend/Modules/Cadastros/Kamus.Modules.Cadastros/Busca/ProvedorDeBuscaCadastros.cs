using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Busca;

/// <summary>Clientes e fornecedores ativos que o perfil pode ver, por nome, documento, telefone ou e-mail.</summary>
internal sealed class ProvedorDeBuscaCadastros(CadastrosDbContext db, IEmpresaAtiva empresa) : IProvedorDeBusca
{
    public async Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(string termo, int limite, CancellationToken ct)
    {
        if (!empresa.Definida) return [];
        var pessoas = (await db.Pessoas.AsNoTracking().Where(p => p.Ativo).ToListAsync(ct))
            .Where(p => PessoasEndpoints.Corresponde(p, termo))
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .ToList();

        var resultados = new List<ResultadoDeBusca>();
        if (Permissoes.PodeClientes(empresa.Perfil))
            resultados.AddRange(pessoas.Where(p => p.EhCliente).Take(limite).Select(p =>
                new ResultadoDeBusca("Clientes", "cliente", p.Id.ToString(), p.Nome, p.DocumentoFormatado ?? p.Telefone, $"/cadastros/clientes?id={p.Id}")));
        if (Permissoes.PodeFornecedores(empresa.Perfil))
            resultados.AddRange(pessoas.Where(p => p.EhFornecedor).Take(limite).Select(p =>
                new ResultadoDeBusca("Fornecedores", "fornecedor", p.Id.ToString(), p.NomeFantasia ?? p.Nome, p.DocumentoFormatado, $"/cadastros/fornecedores?id={p.Id}")));
        return resultados;
    }
}
