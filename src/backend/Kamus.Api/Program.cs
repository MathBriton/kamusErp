using Kamus.Api.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusHealthChecks(builder.Configuration);
builder.Services.AddKamusDocumentacaoApi();

var app = builder.Build();

app.MapKamusDocumentacaoApi();
app.MapHealthChecks("/health");
app.MapVersao();

app.Run();

public partial class Program;
