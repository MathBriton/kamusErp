using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Cadastros.Dominio;

/// <summary>Quem vende (com ou sem login no sistema). A comissão padrão é usada nas vendas (v0.13.0).</summary>
[DisplayName("Vendedor")]
public sealed class Vendedor : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = "";

    /// <summary>Usuário do sistema vinculado (opcional): permite saber "minhas vendas".</summary>
    public Guid? UsuarioId { get; private set; }

    public decimal ComissaoPercentual { get; private set; }
    public string? Telefone { get; private set; }
    public bool Ativo { get; private set; }

    private Vendedor() { }

    public static Vendedor Criar(string nome, decimal comissaoPercentual, Guid? usuarioId, string? telefone)
    {
        var vendedor = new Vendedor { Id = Guid.NewGuid(), Ativo = true };
        vendedor.Atualizar(nome, comissaoPercentual, usuarioId, telefone);
        return vendedor;
    }

    public void Atualizar(string nome, decimal comissaoPercentual, Guid? usuarioId, string? telefone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentOutOfRangeException.ThrowIfNegative(comissaoPercentual);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(comissaoPercentual, 100m);
        Nome = nome.Trim();
        ComissaoPercentual = comissaoPercentual;
        UsuarioId = usuarioId;
        Telefone = Dominio.Telefone.Normalizar(telefone);
    }

    public void Inativar() => Ativo = false;

    public void Reativar() => Ativo = true;
}
