using System.Net;
using System.Text.Json;

namespace Cadence.Api.IntegrationTests.Infrastructure;

public static class ProblemAssertions
{
    /// <summary>Asserts a problem details response with the given status and, optionally, error code.</summary>
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        if (code is not null)
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        }
    }
}
