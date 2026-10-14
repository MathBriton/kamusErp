using System.Collections.Concurrent;
using Kamus.Infrastructure.Planilhas;

namespace Kamus.Infrastructure.Importacao;

/// <summary>Planilha validada aguardando confirmação (30 minutos, em memória). Só quem enviou, no mesmo cliente, confirma.</summary>
internal sealed class SessoesDeImportacao(TimeProvider relogio)
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(30);

    public sealed record Sessao(Guid Id, string Tipo, Guid TenantId, Guid UsuarioId, string NomeDoArquivo, TabelaLida Tabela, DateTimeOffset ExpiraEm);

    private readonly ConcurrentDictionary<Guid, Sessao> _sessoes = new();

    public Sessao Guardar(string tipo, Guid tenantId, Guid usuarioId, string nomeDoArquivo, TabelaLida tabela)
    {
        var agora = relogio.GetUtcNow();
        foreach (var vencida in _sessoes.Values.Where(s => s.ExpiraEm <= agora)) _sessoes.TryRemove(vencida.Id, out _);
        var sessao = new Sessao(Guid.NewGuid(), tipo, tenantId, usuarioId, nomeDoArquivo, tabela, agora + Validade);
        _sessoes[sessao.Id] = sessao;
        return sessao;
    }

    /// <summary>Retira a sessão (uso único). Nulo se não existe, venceu ou é de outro usuário/cliente.</summary>
    public Sessao? Retirar(Guid id, Guid tenantId, Guid usuarioId)
    {
        if (!_sessoes.TryGetValue(id, out var s) || s.TenantId != tenantId || s.UsuarioId != usuarioId) return null;
        _sessoes.TryRemove(id, out _);
        return s.ExpiraEm > relogio.GetUtcNow() ? s : null;
    }
}
