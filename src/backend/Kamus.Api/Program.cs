using Kamus.Api.Infra;
using Kamus.Infrastructure.Auditoria;
using Kamus.Infrastructure.Busca;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Infrastructure.Relatorios;
using Kamus.Modules.Identidade;
using Kamus.Modules.Licenciamento;
using Kamus.Modules.Notificacoes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusMultiTenancy();
builder.Services.AddKamusAuditoria(builder.Configuration);
builder.Services.AddKamusRelatorios(builder.Configuration);
builder.Services.AddKamusHealthChecks(builder.Configuration);
builder.Services.AddKamusDocumentacaoApi();
builder.Services.AddProblemDetails();
builder.Services.AddModuloIdentidade(builder.Configuration);
builder.Services.AddModuloNotificacoes(builder.Configuration);
builder.Services.AddModuloLicenciamento(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseKamusMultiTenancy();
app.UseModuloIdentidade();
app.UseModuloLicenciamento();
app.UseAuthorization();

app.MapKamusDocumentacaoApi();
app.MapHealthChecks("/health");
app.MapVersao();
app.MapKamusAuditoria();
app.MapKamusBusca();
app.MapKamusRelatorios();
app.MapModuloIdentidade();
app.MapModuloNotificacoes();
app.MapModuloLicenciamento();

await app.AplicarMigracoesKamusAsync();
await app.InicializarModuloIdentidadeAsync();
await app.InicializarModuloLicenciamentoAsync();

await app.RunAsync();

public partial class Program;
