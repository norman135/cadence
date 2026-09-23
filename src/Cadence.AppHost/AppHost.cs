// Local development environment: `dotnet run --project src/Cadence.AppHost`
//
// Starts PostgreSQL and Mailpit in containers, runs the migrator, then starts the API once the
// schema is up to date. The Aspire dashboard shows logs, traces and metrics for everything.

var builder = DistributedApplication.CreateBuilder(args);

// The same PostgreSQL major version as production. The container outlives AppHost restarts,
// and its data lives in a named volume, so the inner dev loop stays fast.
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18-alpine")
    .WithVolume("cadence-dev-postgres", "/var/lib/postgresql")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("cadence");

// Catches every email the app sends; the web UI is linked from the dashboard.
var mailpit = builder.AddMailPit("mailpit");

var migrator = builder.AddProject<Projects.Cadence_Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.Cadence_Api>("api")
    .WithReference(database)
    .WithReference(mailpit)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health/ready");

await builder.Build().RunAsync();
