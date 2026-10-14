using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Identidade.Endpoints;

/// <summary>Regras comuns das telas de gestão (empresas e usuários): só o perfil Dono.</summary>
internal static class Gestao
{
    public const string CodigoLimite = "LIMITE_DA_LICENCA";

    public static IResult? ExigirDono(IEmpresaAtiva empresa) =>
        empresa.Perfil == "Dono"
            ? null
            : Problema(StatusCodes.Status403Forbidden, "Sem permissão", "Somente o perfil Dono gerencia empresas e usuários.");

    public static IResult Problema(int status, string titulo, string detalhe) => Results.Problem(statusCode: status, title: titulo, detail: detalhe);

    public static IResult Limite(string oque, int limite) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Limite do plano",
        detail: $"Seu plano permite até {limite} {oque}. Fale com o suporte para ampliar.",
        extensions: new Dictionary<string, object?> { ["codigo"] = CodigoLimite });
}
