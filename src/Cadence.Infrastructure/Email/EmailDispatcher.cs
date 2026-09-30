using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cadence.Infrastructure.Email;

/// <summary>
/// Drains the <see cref="EmailQueue"/> in the background. A failed send is retried with backoff
/// (1 s, 2 s, 4 s), then logged and dropped.
/// </summary>
internal sealed partial class EmailDispatcher(
    EmailQueue queue,
    IEmailTransport transport,
    TimeProvider timeProvider,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    private const int MaxAttempts = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendWithRetryAsync(message, stoppingToken);
        }
    }

    private async Task SendWithRetryAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await transport.SendAsync(message, stoppingToken);
                LogSent(logger, message.Subject);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Any delivery failure is retried and finally logged; it must not stop the dispatcher.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                if (attempt == MaxAttempts)
                {
                    LogFailed(logger, exception, message.Subject, attempt);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), timeProvider, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent email \"{Subject}\"")]
    private static partial void LogSent(ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email \"{Subject}\" after {Attempts} attempts")]
    private static partial void LogFailed(ILogger logger, Exception exception, string subject, int attempts);
}
