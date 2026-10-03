// Applies pending EF Core migrations, then exits.
//
// Runs as a one-shot container (docker compose) or an Aspire resource before the API starts,
// so the API never changes the schema at startup and several API replicas can never race
// each other to migrate. Exit code 0 means the database is up to date.
//
// Development and performance testing only: `seed --issues 10000 --password <password>` also
// creates the Lumen Labs demo organization (see DevelopmentSeeder). The password has no default,
// so seeding never happens by accident.

using Cadence.Infrastructure;
using Cadence.Migrator;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCadenceDbContext();

using var host = builder.Build();
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

var migrated = await MigrationRunner.RunAsync(host.Services, timeout.Token);
if (migrated != 0 || args.FirstOrDefault() != "seed")
{
    return migrated;
}

var password = builder.Configuration["password"];
if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
{
    await Console.Error.WriteLineAsync("seed needs --password with at least 10 characters.");
    return 2;
}

var issues = int.TryParse(builder.Configuration["issues"], System.Globalization.CultureInfo.InvariantCulture, out var count) ? count : 10_000;
using var seedTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
return await DevelopmentSeeder.RunAsync(host.Services, issues, password, seedTimeout.Token);
