using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Benchmarks.Persistence;

/// <summary>
/// How long EF Core takes to build Cadence's model, which happens once per process on the first
/// query. This is what a compiled model (dotnet ef dbcontext optimize) would save at startup; it
/// isn't used because compiled models don't support the tenant query filters (ADR-0007).
/// </summary>
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 10)]
[MemoryDiagnoser]
public class ModelBuildingBenchmarks
{
    private DbContextOptions<CadenceDbContext> _options = null!;

    [GlobalSetup]
    public void Setup() =>
        // No connection is opened: building the model needs only the provider.
        _options = new DbContextOptionsBuilder<CadenceDbContext>()
            .UseNpgsql("Host=localhost;Database=benchmark")
            .UseSnakeCaseNamingConvention()
            // Without caching, every context builds its model from scratch, as a new process does.
            .EnableServiceProviderCaching(false)
            .Options;

    [Benchmark]
    public int BuildModel()
    {
        using var db = new CadenceDbContext(_options);
        return db.Model.GetEntityTypes().Count();
    }
}
