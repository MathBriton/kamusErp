namespace Kamus.SharedKernel.Clientes;

/// <param name="Senha">Senha inicial do Dono (o operador repassa ao cliente).</param>
public sealed record NovoCliente(string Nome, string RazaoSocial, string NomeFantasia, string Cnpj, string NomeDoDono, string EmailDoDono, string Senha);

/// <param name="Erro">Motivo pronto para exibir quando o cadastro foi recusado.</param>
/// <param name="Conflito">Recusado porque algo já existe (ex.: e-mail em uso).</param>
public sealed record ResultadoDoCadastro(Guid? TenantId, string? Erro = null, bool Conflito = false)
{
    public static ResultadoDoCadastro Recusado(string erro, bool conflito = false) => new(null, erro, conflito);
}

/// <summary>Cria um cliente novo: tenant, primeira empresa (CNPJ) e o usuário Dono. Implementado pelo módulo Identidade.</summary>
public interface ICadastroDeClientes
{
    Task<ResultadoDoCadastro> CriarAsync(NovoCliente cliente, CancellationToken ct = default);
}
