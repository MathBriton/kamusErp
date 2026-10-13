using Kamus.Api.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamusHealthChecks(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
