namespace Kamus.Modules.Identidade.Dominio;

/// <summary>Cliente do SaaS (assinante). Agrupa empresas (CNPJs), lojas e usuários.</summary>
public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = "";
    public DateTimeOffset CriadoEm { get; private set; }

    private Tenant() { }

    public static Tenant Criar(string nome, DateTimeOffset agora, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new Tenant { Id = id ?? Guid.NewGuid(), Nome = nome.Trim(), CriadoEm = agora };
    }
}
