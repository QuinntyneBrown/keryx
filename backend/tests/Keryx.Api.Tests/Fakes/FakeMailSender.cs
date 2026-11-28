using System.Collections.Concurrent;
using Keryx.Application.Alerts;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The mail service for the tests: every message sent is kept in order, and a test may make the
/// service fail, as a mail server that refuses or times out would.
/// </summary>
public sealed class FakeMailSender : IMailSender
{
    public ConcurrentQueue<MailMessage> Sent { get; } = new();

    /// <summary>Every send fails while this is set.</summary>
    public bool Failing { get; set; }

    /// <summary>How many sends were attempted, including those that failed.</summary>
    public int Attempts => _attempts;

    private int _attempts;

    public bool IsConfigured { get; set; } = true;

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attempts);
        if (Failing)
        {
            throw new IOException("The mail service refused the connection.");
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
