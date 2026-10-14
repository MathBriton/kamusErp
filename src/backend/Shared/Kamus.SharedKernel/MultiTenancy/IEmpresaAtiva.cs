namespace Kamus.SharedKernel.MultiTenancy;

/// <summary>
/// Empresa (CNPJ) escolhida pelo usuário para a requisição, já validada contra os acessos dele.
/// Na visão consolidada ("todas as empresas", só leitura) <see cref="EmpresaId"/> é nulo e
/// <see cref="EmpresaIds"/> traz todas as empresas consideradas.
/// </summary>
public interface IEmpresaAtiva
{
    bool Definida { get; }
    bool Consolidado { get; }
    Guid? EmpresaId { get; }
    IReadOnlyList<Guid> EmpresaIds { get; }

    /// <summary>Perfil do usuário na empresa ativa ("Dono" na visão consolidada).</summary>
    string? Perfil { get; }

    /// <summary>Nome fantasia, ou "Todas as empresas (N)" na visão consolidada.</summary>
    string? Nome { get; }

    /// <summary>CNPJ formatado; nulo na visão consolidada.</summary>
    string? Cnpj { get; }
}
