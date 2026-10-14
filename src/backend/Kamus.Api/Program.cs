using Kamus.Api.Infra;
using Kamus.Infrastructure.Auditoria;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Identidade;
using Kamus.Modules.Notificacoes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusMultiTenancy();
builder.Services.AddKamusAuditoria(builder.Configuration);
builder.Services.AddKamusHealthChecks(builder.Configuration);
builder.Services.AddKamusDocumentacaoApi();
builder.Services.AddProblemDetails();
builder.Services.AddModuloIdentidade(builder.Configuration);
builder.Services.AddModuloNotificacoes(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseKamusMultiTenancy();
app.UseModuloIdentidade();
app.UseAuthorization();

app.MapKamusDocumentacaoApi();
app.MapHealthChecks("/health");
app.MapVersao();
app.MapKamusAuditoria();
app.MapModuloIdentidade();
app.MapModuloNotificacoes();

await app.AplicarMigracoesKamusAsync();
await app.InicializarModuloIdentidadeAsync();

await app.RunAsync();

public partial class Program;
