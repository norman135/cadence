using System.Threading.Channels;

namespace Cadence.Infrastructure.Email;

/// <summary>An email ready to send. Bodies may contain secrets (sign-in links) and are never logged.</summary>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// In-process queue between request handlers and the <see cref="EmailDispatcher"/>, so requests never
/// wait for the mail server. It is bounded to cap memory use if the server is unreachable.
/// Messages still queued at shutdown are lost; the durable outbox (M8) replaces this queue.
/// </summary>
internal sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(capacity: 500)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(message, cancellationToken);
}
