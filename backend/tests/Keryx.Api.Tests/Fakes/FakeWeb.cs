using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The internet, as far as the real source fetcher can tell: it stands where the sockets would,
/// answers each URL as a test said, and answers 404 to anything else. Every request is recorded
/// and announced, so a test can await one instead of sleeping.
/// </summary>
public sealed class FakeWeb : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes =
        new(StringComparer.Ordinal);

    private readonly Channel<HttpRequestMessage> _arrivals = Channel.CreateUnbounded<HttpRequestMessage>();

    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    public void On(string url, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) => _routes[url] = answer;

    public void On(string url, Func<HttpResponseMessage> answer) => On(url, (_, _) => Task.FromResult(answer()));

    public void Text(string url, string body, string contentType) =>
        On(url, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, contentType) });

    /// <summary>Waits for the next request to <paramref name="url"/>, naming what did arrive if it does not.</summary>
    public async Task<HttpRequestMessage> NextAsync(string url, TimeSpan within)
    {
        using var timeout = new CancellationTokenSource(within);
        var seen = new List<string>();
        try
        {
            while (true)
            {
                var request = await _arrivals.Reader.ReadAsync(timeout.Token);
                if (request.RequestUri?.AbsoluteUri == url)
                {
                    return request;
                }

                seen.Add(request.RequestUri?.AbsoluteUri ?? "?");
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"No request to {url} within {within}; arrived: [{string.Join(", ", seen)}]");
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        await _arrivals.Writer.WriteAsync(request, cancellationToken);
        return _routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var answer)
            ? await answer(request, cancellationToken)
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
