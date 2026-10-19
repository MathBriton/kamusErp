namespace Kamus.Modules.Indicadores.Aplicacao;

/// <summary>Que bloco do dashboard e do resumo cada perfil vê. O Vendedor vê só as vendas que ele registrou.</summary>
internal static class Perfis
{
    public static readonly string[] VendasCompletas = ["Dono", "Gerente", "Financeiro"];
    public static readonly string[] VendasProprias = ["Vendedor"];
    public static readonly string[] Financeiro = ["Dono", "Gerente", "Financeiro"];
    public static readonly string[] Estoque = ["Dono", "Gerente", "Estoquista", "Financeiro"];
    public static readonly string[] Condicionais = ["Dono", "Gerente", "Vendedor", "Financeiro"];
    public static readonly string[] Crediario = ["Dono", "Gerente", "Financeiro"];
}
