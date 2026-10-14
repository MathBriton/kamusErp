using System.ComponentModel;
using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Modules.Estoque.Dominio;

/// <summary>Local físico com estoque próprio (e, no futuro, caixa). Pertence a uma empresa (CNPJ).</summary>
[DisplayName("Loja")]
public sealed class Loja : ITenantOwned
{
    public const int TamanhoMaximoDoNome = 60;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = "";
    public bool Ativa { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }

    private Loja() { }

    public static Loja Criar(Guid empresaId, string nome, DateTimeOffset agora)
    {
        var loja = new Loja { Id = Guid.NewGuid(), EmpresaId = empresaId, Ativa = true, CriadaEm = agora };
        loja.Renomear(nome);
        return loja;
    }

    public void Renomear(string nome)
    {
        var aparado = nome?.Trim() ?? "";
        if (aparado.Length == 0) throw new ArgumentException("Informe o nome da loja.", nameof(nome));
        if (aparado.Length > TamanhoMaximoDoNome) throw new ArgumentException($"O nome da loja tem no máximo {TamanhoMaximoDoNome} caracteres.", nameof(nome));
        Nome = aparado;
    }

    public void Desativar() => Ativa = false;

    public void Reativar() => Ativa = true;
}
