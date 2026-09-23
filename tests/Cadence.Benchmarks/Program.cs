using BenchmarkDotNet.Running;
using Cadence.Benchmarks.Serialization;

BenchmarkSwitcher.FromAssembly(typeof(JsonSerializationBenchmarks).Assembly).Run(args);
