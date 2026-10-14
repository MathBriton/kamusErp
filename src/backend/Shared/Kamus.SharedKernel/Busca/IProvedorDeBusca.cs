namespace Kamus.SharedKernel.Busca;

/// <param name="Grupo">Título do grupo na pesquisa (ex.: "Produtos", "Clientes").</param>
/// <param name="Tipo">Código usado pelo frontend para decidir a ação (ex.: <c>empresa</c> troca o CNPJ ativo).</param>
/// <param name="Link">Rota do frontend aberta ao escolher o resultado.</param>
public sealed record ResultadoDeBusca(string Grupo, string Tipo, string Id, string Titulo, string? Subtitulo, string? Link);

/// <summary>
/// Cada módulo registra o seu provedor; a pesquisa global (Ctrl+K) junta todos.
/// O provedor deve respeitar tenant, empresa ativa e perfil — nunca devolver o que o usuário não pode ver.
/// </summary>
public interface IProvedorDeBusca
{
    Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(string termo, int limite, CancellationToken ct);
}
