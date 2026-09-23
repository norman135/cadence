using System.Text.Json;
using System.Text.Json.Serialization;
using Cadence.Application.Features.System;

namespace Cadence.Api.Serialization;

/// <summary>
/// Compile-time JSON metadata for every API contract. Serializers are generated ahead of time,
/// which removes reflection warm-up and reduces allocations per request.
/// Add each new request and response type here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SystemInfoResponse))]
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext;
