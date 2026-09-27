using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;

namespace Cadence.Api.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class HealthEndpointTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Liveness_does_not_touch_the_database()
    {
        await factory.Queries.AssertAtMostAsync(0, () =>
            _client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken));
    }
}
