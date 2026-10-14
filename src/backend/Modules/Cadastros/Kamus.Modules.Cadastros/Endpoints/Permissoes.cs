using Kamus.Modules.Cadastros.Dominio;
using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Cadastros.Endpoints;

/// <summary>Quem mexe em quê (perfil na empresa ativa). A tela esconde, mas a regra vale aqui.</summary>
internal static class Permissoes
{
    public static readonly string[] Clientes = ["Dono", "Gerente", "Vendedor", "Financeiro"];
    public static readonly string[] Fornecedores = ["Dono", "Gerente", "Estoquista", "Financeiro"];
    public static readonly string[] Gestao = ["Dono", "Gerente"];

    public static bool PodeClientes(string? perfil) => Clientes.Contains(perfil);

    public static bool PodeFornecedores(string? perfil) => Fornecedores.Contains(perfil);

    /// <summary>Precisa poder lidar com todos os papéis envolvidos (os que a pessoa tem e os que vai ter).</summary>
    public static bool PodePapeis(string? perfil, bool cliente, bool fornecedor) =>
        (!cliente || PodeClientes(perfil)) && (!fornecedor || PodeFornecedores(perfil)) && (cliente || fornecedor);

    public static bool PodeVer(string? perfil, Pessoa p) => (p.EhCliente && PodeClientes(perfil)) || (p.EhFornecedor && PodeFornecedores(perfil));

    public static IResult Negado(string detalhe = "Seu perfil não tem acesso a este cadastro.") =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: detalhe);
}
