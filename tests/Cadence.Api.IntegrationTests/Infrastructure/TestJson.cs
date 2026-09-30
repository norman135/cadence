using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>Reads responses with the same conventions the API writes them: camelCase, enums as strings.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<T?> ReadJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(Options, cancellationToken);
}
