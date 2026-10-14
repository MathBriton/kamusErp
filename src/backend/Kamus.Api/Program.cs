using Kamus.Api.Infra;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusMultiTenancy();
builder.Services.AddKamusHealthChecks(builder.Configuration);
builder.Services.AddKamusDocumentacaoApi();
builder.Services.AddProblemDetails();
builder.Services.AddModuloIdentidade(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseKamusMultiTenancy();
app.UseModuloIdentidade();
app.UseAuthorization();

app.MapKamusDocumentacaoApi();
app.MapHealthChecks("/health");
app.MapVersao();
app.MapModuloIdentidade();

await app.InicializarModuloIdentidadeAsync();

await app.RunAsync();

public partial class Program;
