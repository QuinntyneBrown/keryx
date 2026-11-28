using Keryx.Application.Abstractions;
using Keryx.Application.Behaviors;
using Keryx.Application.Telemetry;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keryx.Application.Alerts;

/// <summary>The digest worker's morning request (L2-078).</summary>
public sealed record SendHeldArticlesDigestCommand : IRequest, IWorkerRunCommand
{
    public RunWorker Worker => RunWorker.AlertDigest;
}

/// <summary>
/// Records a run with worker <c>alert_digest</c> every time (L2-078 AC4). With the digest switch
/// on, an alert address, a mail service and articles in Needs review, it sends one e-mail with
/// their count and the newest 20 titles; otherwise it sends nothing. The run records no articles,
/// so its counts are 0; a refused send leaves it completed with an <c>alert_not_sent</c> warning,
/// and the next morning's digest replaces it.
/// </summary>
internal sealed partial class SendHeldArticlesDigestCommandHandler(
    IAppDbContext db, IMailSender mail, AlertComposer composer, AlertNotSentTracker notSent, TimeProvider time,
    ILogger<SendHeldArticlesDigestCommandHandler> logger)
    : IRequestHandler<SendHeldArticlesDigestCommand>
{
    internal const string NotSentDetail = "The daily digest could not be sent.";

    public async Task Handle(SendHeldArticlesDigestCommand request, CancellationToken cancellationToken)
    {
        var run = Run.Start(RunWorker.AlertDigest, RunTrigger.Schedule, time.GetUtcNow());
        db.Runs.Add(run);
        RunTelemetry.Covers(run);
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.AlertDigest, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            lease = RunLease.For(RunWorker.AlertDigest, time.GetUtcNow());
            db.RunLeases.Add(lease);
        }

        // The lease names the run, so a digest that throws is still recorded (L2-019 AC3).
        lease.Claim(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var refused = 0;
        var sent = 0;
        var settings = await db.Settings.SingleAsync(cancellationToken).ConfigureAwait(false);
        if (settings.DailyDigest && mail.IsConfigured && !string.IsNullOrWhiteSpace(settings.AlertEmail))
        {
            var held = db.Articles.Where(article => article.Status == PublicationStatus.NeedsReview);
            var count = await held.CountAsync(cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                var titles = await held.OrderByDescending(article => article.ListedAt).ThenByDescending(article => article.Id)
                    .Take(AlertComposer.DigestTitles).Select(article => article.Title).ToListAsync(cancellationToken).ConfigureAwait(false);
                // Composed before the send: only the mail service's refusal is a refused send.
                var message = composer.Digest(settings.AlertEmail, count, titles);
                try
                {
                    await mail.SendAsync(message, cancellationToken).ConfigureAwait(false);
                    sent++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // The type only: a mail server's message may name its host or the address.
                    DigestNotSent(logger, exception.GetType().Name);
                    refused++;
                }
            }
        }

        await notSent.TrackAsync(run, refused, sent, NotSentDetail, cancellationToken).ConfigureAwait(false);
        run.Finish(new RunCounts(), time.GetUtcNow(), withIssues: refused > 0);
        lease.Release(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The daily digest could not be sent: {ExceptionType}.")]
    private static partial void DigestNotSent(ILogger logger, string exceptionType);
}
