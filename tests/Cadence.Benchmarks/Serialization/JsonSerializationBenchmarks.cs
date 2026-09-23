using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cadence.Api.Serialization;
using Cadence.Application.Features.System;

namespace Cadence.Benchmarks.Serialization;

/// <summary>
/// Compares reflection-based JSON serialization with the source-generated context the API uses
/// (see ADR-0005). Run it whenever the serialization setup changes.
/// </summary>
[MemoryDiagnoser]
public class JsonSerializationBenchmarks
{
    private static readonly SystemInfoResponse s_response =
        new("Cadence", "1.0.0", "Production", new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

    private static readonly JsonSerializerOptions s_reflectionOptions = new(JsonSerializerDefaults.Web);

    [Benchmark(Baseline = true)]
    public string Reflection() => JsonSerializer.Serialize(s_response, s_reflectionOptions);

    [Benchmark]
    public string SourceGenerated() =>
        JsonSerializer.Serialize(s_response, ApiJsonSerializerContext.Default.SystemInfoResponse);
}
