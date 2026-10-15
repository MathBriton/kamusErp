using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Financeiro.Endpoints;

/// <summary>O financeiro é restrito: Dono, Gerente e Financeiro (perfil na empresa ativa).</summary>
internal static class Permissoes
{
    public static readonly string[] Financeiro = ["Dono", "Gerente", "Financeiro"];

    public static IResult? Barrar(IEmpresaAtiva empresa) =>
        Financeiro.Contains(empresa.Perfil) ? null : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão",
            detail: "Só Dono, Gerente e Financeiro acessam o financeiro.");

    public static IResult Problema(int status, string detalhe) => Results.Problem(statusCode: status, title: "Financeiro", detail: detalhe);

    public static IResult Erro(string detalhe) => Problema(StatusCodes.Status400BadRequest, detalhe);

    public static string Mensagem(Exception ex) => ex.Message.Split(" (Parameter")[0];
}
