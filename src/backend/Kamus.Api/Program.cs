using Kamus.Api.Infra;
using Kamus.Infrastructure.Auditoria;
using Kamus.Infrastructure.Busca;
using Kamus.Infrastructure.Importacao;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Infrastructure.Persistence;
using Kamus.Infrastructure.Relatorios;
using Kamus.Modules.Cadastros;
using Kamus.Modules.Estoque;
using Kamus.Modules.Financeiro;
using Kamus.Modules.Identidade;
using Kamus.Modules.Indicadores;
using Kamus.Modules.Licenciamento;
using Kamus.Modules.Notificacoes;
using Kamus.Modules.Vendas;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusMultiTenancy();
builder.Services.AddKamusAuditoria(builder.Configuration);
builder.Services.AddKamusRelatorios(builder.Configuration);
builder.Services.AddKamusImportacao();
builder.Services.AddKamusHealthChecks(builder.Configuration);
builder.Services.AddKamusDocumentacaoApi();
builder.Services.AddProblemDetails();
builder.Services.AddKamusCors(builder.Configuration);
builder.Services.AddModuloIdentidade(builder.Configuration);
builder.Services.AddModuloNotificacoes(builder.Configuration);
builder.Services.AddModuloLicenciamento(builder.Configuration);
builder.Services.AddModuloCadastros(builder.Configuration);
builder.Services.AddModuloEstoque(builder.Configuration);
builder.Services.AddModuloFinanceiro(builder.Configuration);
builder.Services.AddModuloVendas(builder.Configuration);
builder.Services.AddModuloIndicadores(builder.Configuration);

var app = builder.Build();

app.UseKamusCors();
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
app.MapKamusImportacao();
app.MapModuloIdentidade();
app.MapModuloNotificacoes();
app.MapModuloLicenciamento();
app.MapModuloCadastros();
app.MapModuloEstoque();
app.MapModuloFinanceiro();
app.MapModuloVendas();
app.MapModuloIndicadores();

await app.AplicarMigracoesKamusAsync();
await app.InicializarModuloIdentidadeAsync();
await app.InicializarModuloLicenciamentoAsync();

await app.RunAsync();

public partial class Program;
