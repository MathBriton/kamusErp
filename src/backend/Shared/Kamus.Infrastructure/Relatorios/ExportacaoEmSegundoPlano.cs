using System.Collections.Concurrent;
using System.Threading.Channels;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.Notificacoes;
using Kamus.SharedKernel.Relatorios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Infrastructure.Relatorios;

public sealed class RelatoriosOptions
{
    public const string Secao = "Relatorios";

    /// <summary>Acima deste número de linhas, o arquivo é gerado em segundo plano e avisado no sininho.</summary>
    public int LimiteSincrono { get; set; } = 2_000;

    public int MinutosDeValidadeDoArquivo { get; set; } = 60;
}

internal sealed record PedidoDeExportacao(
    Guid TenantId, Guid UsuarioId, Guid? EmpresaId, DadosDoRelatorio Dados, CabecalhoDoRelatorio Cabecalho, string Formato, string NomeDoArquivo);

internal sealed record ArquivoExportado(string Nome, string ContentType, byte[] Conteudo, DateTimeOffset ExpiraEm);

internal sealed class FilaDeExportacao
{
    private readonly Channel<PedidoDeExportacao> _canal = Channel.CreateUnbounded<PedidoDeExportacao>();

    public ValueTask EnfileirarAsync(PedidoDeExportacao pedido, CancellationToken ct) => _canal.Writer.WriteAsync(pedido, ct);

    public IAsyncEnumerable<PedidoDeExportacao> LerTodosAsync(CancellationToken ct) => _canal.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Arquivos prontos ficam em memória por tempo limitado. O id (aleatório, 122 bits) é o próprio "segredo" do link
/// entregue só ao dono via notificação. Quando houver servidor, trocar por armazenamento em disco/objeto.
/// </summary>
internal sealed class ArquivosExportados(TimeProvider relogio)
{
    private readonly ConcurrentDictionary<Guid, ArquivoExportado> _arquivos = new();

    public Guid Guardar(ArquivoExportado arquivo)
    {
        RemoverExpirados();
        var id = Guid.NewGuid();
        _arquivos[id] = arquivo;
        return id;
    }

    public ArquivoExportado? Obter(Guid id)
    {
        RemoverExpirados();
        return _arquivos.TryGetValue(id, out var arquivo) ? arquivo : null;
    }

    private void RemoverExpirados()
    {
        var agora = relogio.GetUtcNow();
        foreach (var (id, arquivo) in _arquivos)
            if (arquivo.ExpiraEm <= agora) _arquivos.TryRemove(id, out _);
    }
}

internal sealed partial class ExportacaoEmSegundoPlano(
    FilaDeExportacao fila,
    ArquivosExportados arquivos,
    IEnumerable<IExportadorDeRelatorio> exportadores,
    IServiceScopeFactory escopos,
    IOptions<RelatoriosOptions> opcoes,
    TimeProvider relogio,
    ILogger<ExportacaoEmSegundoPlano> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var pedido in fila.LerTodosAsync(stoppingToken))
        {
            try
            {
                await ProcessarAsync(pedido, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                FalhaNaExportacao(log, ex, pedido.Dados.Titulo);
                await NotificarAsync(pedido, new NovaNotificacao(pedido.UsuarioId, "relatorio_falhou", "Não foi possível gerar o relatório",
                    $"\"{pedido.Dados.Titulo}\" ({pedido.Formato.ToUpperInvariant()}) falhou. Tente novamente com menos filtros.", null, pedido.EmpresaId), stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(PedidoDeExportacao pedido, CancellationToken ct)
    {
        var exportador = exportadores.Single(e => e.Formato == pedido.Formato);
        var conteudo = exportador.Exportar(pedido.Dados, pedido.Cabecalho);
        var validade = TimeSpan.FromMinutes(opcoes.Value.MinutosDeValidadeDoArquivo);
        var id = arquivos.Guardar(new ArquivoExportado(pedido.NomeDoArquivo, exportador.ContentType, conteudo, relogio.GetUtcNow() + validade));

        await NotificarAsync(pedido, new NovaNotificacao(pedido.UsuarioId, "relatorio_pronto", "Relatório pronto para baixar",
            $"\"{pedido.Dados.Titulo}\" ({pedido.Formato.ToUpperInvariant()}, {pedido.Dados.Linhas.Count} linhas). Disponível por {validade.TotalMinutes:0} minutos.",
            $"/api/relatorios/arquivos/{id}", pedido.EmpresaId), ct);
    }

    private async Task NotificarAsync(PedidoDeExportacao pedido, NovaNotificacao notificacao, CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(pedido.TenantId);
        await escopo.ServiceProvider.GetRequiredService<INotificador>().NotificarAsync(notificacao, ct);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gerar o relatório {Relatorio} em segundo plano")]
    private static partial void FalhaNaExportacao(ILogger logger, Exception ex, string relatorio);
}
