using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;
using Microsoft.Extensions.Options;

namespace Keryx.Infrastructure.Sources;

/// <summary>
/// Fetches sources as a good and safe citizen (L2-021 to L2-024). It reads robots.txt first and
/// keeps it for 24 hours; sends one request at a time to a host, at least 1 second (or the
/// site's Crawl-delay, up to 30) after the last; fetches at most 8 hosts at once; gives each
/// attempt the source's timeout from the injected clock; makes three attempts at most, backing
/// off 1 then 2 seconds, or longer when the host says Retry-After (up to 60); follows at most 5
/// redirects itself, never from https to http and never to a host that is not public; and reads
/// at most 5 MB of a response whose type the source's kind expects. robots.txt and the hops of a
/// redirect belong to the fetch that needed them and are not spaced from it.
/// </summary>
public sealed class HttpSourceFetcher : ISourceFetcher, IDisposable
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRedirects = 5;
    public const int MaxAttempts = 3;
    private const string Agent = "Keryx";

    private static readonly TimeSpan Spacing = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RobotsLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private static readonly Dictionary<SourceKind, string[]> ExpectedTypes = new Dictionary<SourceKind, string[]>
    {
        [SourceKind.Rss] = ["application/rss+xml", "application/atom+xml", "application/rdf+xml", "application/xml", "text/xml"],
        [SourceKind.Website] = ["text/html", "application/xhtml+xml"],
        [SourceKind.Instagram] = ["application/json"],
        [SourceKind.Eventbrite] = ["application/json"],
        [SourceKind.NewsQuery] = ["application/json", "application/rss+xml", "application/xml", "text/xml"],
    };

    private readonly HttpClient _client;
    private readonly SafeAddressCheck _safety;
    private readonly TimeProvider _time;
    private readonly ProductInfoHeaderValue _product;
    private readonly ProductInfoHeaderValue _contact;
    private readonly SemaphoreSlim _hosts = new(8, 8);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSent = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (RobotsRules Rules, DateTimeOffset FetchedAt)> _robots = new(StringComparer.OrdinalIgnoreCase);

    public HttpSourceFetcher(SourceSockets sockets, IHostResolver resolver, TimeProvider time, IOptions<FetchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(sockets);
        ArgumentNullException.ThrowIfNull(options);
        _client = new HttpClient(sockets.Handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _safety = new SafeAddressCheck(resolver);
        _time = time;
        var version = typeof(HttpSourceFetcher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0";
        _product = new ProductInfoHeaderValue(Agent, version);
        _contact = new ProductInfoHeaderValue($"(+{options.Value.UserAgentContactUrl})");
    }

    public async Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = request.Address.IdnHost;
        await _hosts.WaitAsync(cancellationToken).ConfigureAwait(false);
        var gate = _hostGates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await FetchWithRetriesAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        finally
        {
            _hosts.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _hosts.Dispose();
        foreach (var gate in _hostGates.Values)
        {
            gate.Dispose();
        }
    }

    private async Task<SourceFetchResult> FetchWithRetriesAsync(SourceFetchRequest request, CancellationToken cancellationToken)
    {
        var last = SourceFetchResult.Failed(FetchFailureReason.Refused);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (result, retry, retryAfter) = await AttemptAsync(request, cancellationToken).ConfigureAwait(false);
            if (!retry || attempt == MaxAttempts)
            {
                return result;
            }

            last = result;
            var backOff = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            var wait = retryAfter is { } after && after > backOff ? (after < MaxRetryAfter ? after : MaxRetryAfter) : backOff;
            await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
        }

        return last;
    }

    private async Task<(SourceFetchResult Result, bool Retry, TimeSpan? RetryAfter)> AttemptAsync(SourceFetchRequest request, CancellationToken cancellationToken)
    {
        var address = request.Address;
        var robots = await RobotsAsync(address, request.Timeout, cancellationToken).ConfigureAwait(false);
        if (robots.Failure is { } robotsFailure)
        {
            return (SourceFetchResult.Failed(robotsFailure), false, null);
        }

        await SpaceAsync(address.IdnHost, robots.Rules!.CrawlDelay, cancellationToken).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(request.Timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            for (var hop = 0; ; hop++)
            {
                if (hop > 0)
                {
                    var hopRobots = await RobotsAsync(address, request.Timeout, linked.Token).ConfigureAwait(false);
                    if (hopRobots.Failure is { } failure)
                    {
                        return (SourceFetchResult.Failed(failure), false, null);
                    }

                    robots = hopRobots;
                }

                if (!robots.Rules!.Allows(address.PathAndQuery))
                {
                    return (SourceFetchResult.Failed(FetchFailureReason.BlockedByRobots), false, null);
                }

                if (!await _safety.IsSafeAsync(address, linked.Token).ConfigureAwait(false))
                {
                    return (SourceFetchResult.Failed(FetchFailureReason.UnsafeAddress), false, null);
                }

                using var message = new HttpRequestMessage(HttpMethod.Get, address);
                message.Headers.UserAgent.Add(_product);
                message.Headers.UserAgent.Add(_contact);
                if (hop == 0 && request.ETag is { } etag && EntityTagHeaderValue.TryParse(etag, out var tag))
                {
                    message.Headers.IfNoneMatch.Add(tag);
                }

                if (hop == 0 && request.LastModified is { } modified)
                {
                    message.Headers.IfModifiedSince = modified;
                }

                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                if (IsRedirect(response.StatusCode))
                {
                    if (hop == MaxRedirects || response.Headers.Location is null)
                    {
                        return (SourceFetchResult.Failed(FetchFailureReason.Refused), false, null);
                    }

                    var next = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(address, response.Headers.Location);
                    if ((next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps)
                        || (address.Scheme == Uri.UriSchemeHttps && next.Scheme == Uri.UriSchemeHttp))
                    {
                        return (SourceFetchResult.Failed(FetchFailureReason.Refused), false, null);
                    }

                    address = next;
                    continue;
                }

                return await ReadAsync(request.Kind, response, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (SourceFetchResult.Failed(FetchFailureReason.Timeout), true, null);
        }
        catch (HttpRequestException exception) when (exception.InnerException is UnsafeConnectionException)
        {
            return (SourceFetchResult.Failed(FetchFailureReason.UnsafeAddress), false, null);
        }
        catch (HttpRequestException)
        {
            return (SourceFetchResult.Failed(FetchFailureReason.Refused), true, null);
        }
    }

    private async Task<(SourceFetchResult, bool, TimeSpan?)> ReadAsync(SourceKind kind, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotModified:
                return (SourceFetchResult.Unchanged(), false, null);
            case HttpStatusCode.NotFound or HttpStatusCode.Gone:
                return (SourceFetchResult.Failed(FetchFailureReason.NotFound), false, null);
            case HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable:
                return (SourceFetchResult.Failed(FetchFailureReason.Refused), true, RetryAfter(response));
            case >= HttpStatusCode.InternalServerError:
                return (SourceFetchResult.Failed(FetchFailureReason.Refused), true, null);
            case < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices:
                return (SourceFetchResult.Failed(FetchFailureReason.Refused), false, null);
        }

        var type = response.Content.Headers.ContentType?.MediaType;
        if (type is null || !ExpectedTypes[kind].Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            return (SourceFetchResult.Failed(FetchFailureReason.InvalidContent), false, null);
        }

        if (AsksNotToBeIndexed(response))
        {
            // Nothing from the page may be stored, so its body is not even read (L2-080 AC4).
            return (SourceFetchResult.Fetched(string.Empty, type) with { NoIndex = true }, false, null);
        }

        var content = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        return content is null
            ? (SourceFetchResult.Failed(FetchFailureReason.InvalidContent), false, null)
            : (SourceFetchResult.Fetched(content, type), false, null);
    }

    /// <summary>
    /// Whether <c>X-Robots-Tag</c> holds <c>noindex</c> or <c>none</c>, for every crawler or for
    /// Keryx by name (<c>keryx: noindex</c>); a directive for another crawler does not apply.
    /// </summary>
    private static bool AsksNotToBeIndexed(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Robots-Tag", out var values) && values
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(directive => directive.StartsWith("keryx:", StringComparison.OrdinalIgnoreCase) ? directive["keryx:".Length..].Trim() : directive)
            .Any(directive => directive.Equals("noindex", StringComparison.OrdinalIgnoreCase) || directive.Equals("none", StringComparison.OrdinalIgnoreCase));

    /// <summary>The body as text, or null past 5 MB; decompressed bytes are what is counted.</summary>
    private static async Task<string?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            var charset = content.Headers.ContentType?.CharSet?.Trim('"');
            Encoding encoding;
            try
            {
                encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }

    private async Task<(RobotsRules? Rules, FetchFailureReason? Failure)> RobotsAsync(Uri address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var origin = address.GetLeftPart(UriPartial.Authority);
        var now = _time.GetUtcNow();
        if (_robots.TryGetValue(origin, out var cached) && now - cached.FetchedAt < RobotsLifetime)
        {
            return (cached.Rules, null);
        }

        var robotsAddress = new Uri(origin + "/robots.txt");
        if (!await _safety.IsSafeAsync(robotsAddress, cancellationToken).ConfigureAwait(false))
        {
            return (null, FetchFailureReason.UnsafeAddress);
        }

        using var deadline = new CancellationTokenSource(timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        RobotsRules rules;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, robotsAddress);
            message.Headers.UserAgent.Add(_product);
            message.Headers.UserAgent.Add(_contact);
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            if ((int)response.StatusCode >= 500)
            {
                // A site whose robots.txt cannot be read is not fetched (L2-021 AC2).
                return (null, FetchFailureReason.Refused);
            }

            // No robots.txt (4xx) allows everything; a redirect is not followed and allows it too.
            rules = response.IsSuccessStatusCode
                ? RobotsRules.Parse(await ReadBoundedAsync(response.Content, linked.Token).ConfigureAwait(false) ?? string.Empty, Agent)
                : RobotsRules.AllowAll;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (null, FetchFailureReason.Timeout);
        }
        catch (HttpRequestException exception) when (exception.InnerException is UnsafeConnectionException)
        {
            return (null, FetchFailureReason.UnsafeAddress);
        }
        catch (HttpRequestException)
        {
            return (null, FetchFailureReason.Refused);
        }

        _robots[origin] = (rules, now);
        return (rules, null);
    }

    private async Task SpaceAsync(string host, TimeSpan? crawlDelay, CancellationToken cancellationToken)
    {
        var spacing = crawlDelay is { } delay && delay > Spacing ? delay : Spacing;
        if (_lastSent.TryGetValue(host, out var last))
        {
            var wait = last + spacing - _time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
            }
        }

        _lastSent[host] = _time.GetUtcNow();
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private TimeSpan? RetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - _time.GetUtcNow(),
            _ => null,
        };
}
