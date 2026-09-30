using Cadence.Api.Endpoints;
using Cadence.Api.Hosting;
using Cadence.Application;
using Cadence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.UsePlaceholderSettings();
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseApi();
app.MapDefaultEndpoints();
app.MapApiEndpoints();
app.MapSpa();

await app.RunAsync();
