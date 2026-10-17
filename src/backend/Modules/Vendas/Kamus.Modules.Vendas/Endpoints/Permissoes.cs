using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Vendas.Endpoints;

/// <summary>Quem faz o quê nas vendas (perfil na empresa ativa).</summary>
internal static class Permissoes
{
    /// <summary>Registram vendas. O Vendedor só vê as que ele mesmo registrou.</summary>
    public static readonly string[] Registrar = ["Dono", "Gerente", "Vendedor"];

    /// <summary>Cancelam vendas e configuram formas de pagamento e regras.</summary>
    public static readonly string[] Gestao = ["Dono", "Gerente"];

    /// <summary>Veem todas as vendas, comissões, custo e lucro.</summary>
    public static readonly string[] Consultar = ["Dono", "Gerente", "Financeiro"];

    public static bool VeTudo(IEmpresaAtiva empresa) => Consultar.Contains(empresa.Perfil);

    public static IResult? Exigir(IEmpresaAtiva empresa, string[] perfis, string detalhe) =>
        perfis.Contains(empresa.Perfil) ? null : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: detalhe);

    public static IResult Problema(int status, string detalhe) => Results.Problem(statusCode: status, title: "Vendas", detail: detalhe);

    public static IResult Erro(string detalhe) => Problema(StatusCodes.Status400BadRequest, detalhe);

    public static string Mensagem(Exception ex) => ex.Message.Split(" (Parameter")[0];
}
