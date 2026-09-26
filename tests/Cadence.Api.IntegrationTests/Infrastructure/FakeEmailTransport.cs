using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cadence.Infrastructure.Email;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>Captures outgoing emails instead of sending them, so tests can follow the links inside.</summary>
public sealed partial class FakeEmailTransport : IEmailTransport
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Waits for the latest email to <paramref name="to"/> whose subject contains <paramref name="subject"/>.</summary>
    public async Task<EmailMessage> WaitForEmailAsync(string to, string subject)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var match = _sent.LastOrDefault(message =>
                message.ToAddress.Equals(to, StringComparison.OrdinalIgnoreCase)
                && message.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }

            await Task.Delay(25, timeout.Token);
        }
    }

    /// <summary>Extracts a query parameter from the first link in the email's text body.</summary>
    public static string LinkParameter(EmailMessage message, string name)
    {
        var link = LinkPattern().Match(message.TextBody).Value;
        var query = new Uri(link).Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));

        return query[name];
    }

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
