// Local development environment: `dotnet run --project src/Cadence.AppHost`
//
// Starts PostgreSQL and Mailpit in containers, runs the migrator, starts the API once the
// schema is up to date, then the Vite dev server. The Aspire dashboard shows logs, traces and
// metrics for everything.

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

var api = builder.AddProject<Projects.Cadence_Api>("api")
    .WithReference(database)
    .WithReference(mailpit)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health/ready");

// Vite dev server with hot reload. It proxies /api to the API through service discovery,
// so the browser sees a single origin, as it does in production.
builder.AddViteApp("web", "../../web")
    .WithReference(api)
    .WaitFor(api)
    // A fixed port, so links in development emails (Cadence:PublicUrl) always point at the app.
    .WithEndpoint("http", endpoint => endpoint.Port = 5173);

await builder.Build().RunAsync();
