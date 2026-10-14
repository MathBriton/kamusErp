using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Estoque.Endpoints;

/// <summary>Quem faz o quê no estoque (perfil na empresa ativa). Todos os perfis consultam a posição (sem custos).</summary>
internal static class Permissoes
{
    /// <summary>Lançam entradas, ajustes, transferências e estoque inicial.</summary>
    public static readonly string[] Movimentar = ["Dono", "Gerente", "Estoquista"];

    /// <summary>Veem custo médio, valores em custo e o kardex.</summary>
    public static readonly string[] VerCusto = ["Dono", "Gerente", "Estoquista", "Financeiro"];

    public static bool PodeMovimentar(string? perfil) => Movimentar.Contains(perfil);

    public static bool PodeVerCusto(string? perfil) => VerCusto.Contains(perfil);

    public static IResult Negado(string detalhe) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão", detail: detalhe);

    public static IResult Problema(int status, string detalhe, string? codigo = null) => Results.Problem(
        statusCode: status, title: "Estoque", detail: detalhe,
        extensions: codigo is null ? null : new Dictionary<string, object?> { ["codigo"] = codigo });
}
