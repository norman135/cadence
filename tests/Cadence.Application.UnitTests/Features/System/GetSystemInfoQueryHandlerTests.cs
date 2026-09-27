using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.System;
using Microsoft.Extensions.Time.Testing;

namespace Cadence.Application.UnitTests.Features.System;

public sealed class GetSystemInfoQueryHandlerTests
{
    private sealed class StubApplicationInfo : IApplicationInfo
    {
        public string Name => "Cadence";

        public string Version => "1.2.3";

        public string Environment => "Production";
    }

    [Fact]
    public async Task Returns_application_details_and_the_current_utc_time()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var handler = new GetSystemInfoQueryHandler(new StubApplicationInfo(), new FakeTimeProvider(now));

        var response = await handler.Handle(new GetSystemInfoQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(new SystemInfoResponse("Cadence", "1.2.3", "Production", now), response);
    }
}
