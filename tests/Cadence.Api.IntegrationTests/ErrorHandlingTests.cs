using System.Net;
using System.Text.Json;
using Cadence.Api.IntegrationTests.Infrastructure;

namespace Cadence.Api.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ErrorHandlingTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/unversioned")]
    public async Task Unknown_api_routes_return_problem_details(string path)
    {
        var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"GET {path}", problem.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("traceId").GetString()));
    }
}
