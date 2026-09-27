// Applies pending EF Core migrations, then exits.
//
// Runs as a one-shot container (docker compose) or an Aspire resource before the API starts,
// so the API never changes the schema at startup and several API replicas can never race
// each other to migrate. Exit code 0 means the database is up to date.

using Cadence.Infrastructure;
using Cadence.Migrator;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCadenceDbContext();

using var host = builder.Build();
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

return await MigrationRunner.RunAsync(host.Services, timeout.Token);
