namespace Kamus.SharedKernel.Auditoria;

/// <summary>Quem está executando a operação. Sem usuário (ex.: dados de demonstração) = "Sistema".</summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }
    string? Nome { get; }
    string? Ip { get; }
}
