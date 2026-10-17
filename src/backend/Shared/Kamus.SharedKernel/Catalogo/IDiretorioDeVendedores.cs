namespace Kamus.SharedKernel.Catalogo;

public sealed record VendedorResumo(Guid Id, string Nome, decimal ComissaoPercentual, bool Ativo, Guid? UsuarioId);

/// <summary>Vendedores do cliente atual para outros módulos (comissão nas vendas). Implementado pelo módulo Cadastros.</summary>
public interface IDiretorioDeVendedores
{
    Task<VendedorResumo?> ObterAsync(Guid id, CancellationToken ct = default);
}
