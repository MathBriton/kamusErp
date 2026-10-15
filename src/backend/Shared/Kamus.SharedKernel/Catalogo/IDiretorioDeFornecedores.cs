namespace Kamus.SharedKernel.Catalogo;

public sealed record FornecedorResumo(Guid Id, string Nome, string Documento);

/// <param name="Documento">CNPJ (ou CPF) só com os dígitos/caracteres, sem máscara.</param>
public sealed record NovoFornecedor(string Documento, string RazaoSocial, string? NomeFantasia, string? Telefone, string? Cep, string? Logradouro,
    string? Numero, string? Bairro, string? Cidade, string? Uf);

/// <summary>Fornecedores do cliente atual para outros módulos (ex.: entrada por XML da NF-e). Implementado pelo módulo Cadastros.</summary>
public interface IDiretorioDeFornecedores
{
    Task<FornecedorResumo?> PorDocumentoAsync(string documento, CancellationToken ct = default);

    /// <summary>Devolve o fornecedor com esse documento; se for só cliente, ganha o papel de fornecedor; se não existir, é cadastrado.</summary>
    Task<FornecedorResumo> GarantirAsync(NovoFornecedor dados, CancellationToken ct = default);
}
