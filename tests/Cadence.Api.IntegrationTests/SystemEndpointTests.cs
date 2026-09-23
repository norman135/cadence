using System.Net;
using System.Net.Http.Json;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.System;

namespace Cadence.Api.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class SystemEndpointTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_system_info_returns_application_details()
    {
        var response = await factory.Queries.AssertAtMostAsync(0, () =>
            _client.GetAsync(new Uri("/api/v1/system/info", UriKind.Relative), TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("api-supported-versions"));

        var info = await response.Content.ReadFromJsonAsync<SystemInfoResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(info);
        Assert.Equal("Cadence", info.Name);
        Assert.Equal("Testing", info.Environment);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.InRange(info.ServerTime, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
