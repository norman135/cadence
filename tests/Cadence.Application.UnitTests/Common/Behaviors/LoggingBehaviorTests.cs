using Cadence.Application.Common.Behaviors;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Cadence.Application.UnitTests.Common.Behaviors;

public sealed class LoggingBehaviorTests
{
    public sealed record Ping : IQuery<string>;

    private readonly FakeTimeProvider _time = new();
    private readonly FakeLogger<LoggingBehavior<Ping, string>> _logger = new();

    private MessageHandlerDelegate<Ping, string> HandlerTaking(TimeSpan duration) =>
        (_, _) =>
        {
            _time.Advance(duration);
            return ValueTask.FromResult("pong");
        };

    [Fact]
    public async Task Fast_requests_are_logged_at_debug_level()
    {
        var behavior = new LoggingBehavior<Ping, string>(_logger, _time);

        var response = await behavior.Handle(
            new Ping(),
            HandlerTaking(TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken);

        Assert.Equal("pong", response);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Contains("Ping", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slow_requests_are_logged_as_warnings()
    {
        var behavior = new LoggingBehavior<Ping, string>(_logger, _time);

        await behavior.Handle(
            new Ping(),
            HandlerTaking(LoggingBehavior<Ping, string>.SlowRequestThreshold + TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith("Slow request: Ping", record.Message, StringComparison.Ordinal);
    }
}
