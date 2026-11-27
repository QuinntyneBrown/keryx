using System.Collections.Concurrent;
using Keryx.Application.Sources;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The sources' side of the network for the tests: each address answers what a test gave it, and
/// any other address is not found. Every request is recorded, so a test can prove none was made.
/// </summary>
public sealed class FakeSourceFetcher : ISourceFetcher
{
    private readonly ConcurrentDictionary<string, SourceFetchResult> _answers = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, bool> _throwing = new(StringComparer.Ordinal);

    public ConcurrentQueue<SourceFetchRequest> Requests { get; } = new();

    public void Answer(string address, SourceFetchResult result) => _answers[address] = result;

    /// <summary>The address throws on fetch, as a client bug or an unexpected fault would (L2-008 AC1).</summary>
    public void Throw(string address) => _throwing[address] = true;

    public void AnswerFeed(string address, string xml) =>
        Answer(address, SourceFetchResult.Fetched(xml, "application/rss+xml"));

    public Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        if (_throwing.ContainsKey(request.Address.OriginalString))
        {
            throw new InvalidOperationException($"The fake source at {request.Address} broke.");
        }

        return Task.FromResult(_answers.TryGetValue(request.Address.OriginalString, out var result)
            ? result
            : SourceFetchResult.Failed(FetchFailureReason.NotFound));
    }
}
