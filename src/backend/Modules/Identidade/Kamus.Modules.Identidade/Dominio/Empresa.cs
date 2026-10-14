using Kamus.SharedKernel.Documentos;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Identidade.Dominio;

/// <summary>Empresa (CNPJ) de um tenant. Um tenant pode ter várias.</summary>
public sealed class Empresa : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string RazaoSocial { get; private set; } = "";
    public string NomeFantasia { get; private set; } = "";
    public Cnpj Cnpj { get; private set; } = null!;

    private Empresa() { }

    public static Empresa Criar(string razaoSocial, string nomeFantasia, Cnpj cnpj, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razaoSocial);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeFantasia);
        ArgumentNullException.ThrowIfNull(cnpj);
        return new Empresa { Id = id ?? Guid.NewGuid(), RazaoSocial = razaoSocial.Trim(), NomeFantasia = nomeFantasia.Trim(), Cnpj = cnpj };
    }
}
