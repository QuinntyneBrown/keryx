using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;

using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-020 AC2, AC3, AC4
// Description: Staff mute an open issue: it is no longer counted as open and is still listed on
// its run, muted. Unmuting opens it again. Muting twice, or unmuting an open issue, changes
// nothing; an issue that is over cannot be muted; an unknown issue is not found. Each test has
// its own store.
public sealed class MuteIssueTests
{
    private const string Gone = "https://gone.example/feed";
    private static readonly Uri Open = new("/admin/v1/issues?state=open", UriKind.Relative);

    [Fact]
    public async Task Given_an_open_issue_When_staff_mute_it_Then_it_is_not_open_and_its_run_lists_it_muted()
    {
        using var factory = new ApiFactory();
        var (runId, issueId) = await FailingSourceAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/mute");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var issue = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((issueId, "muted", true, "source_not_found"), (
            issue.GetProperty("id").GetString(),
            issue.GetProperty("state").GetString(),
            issue.GetProperty("muted").GetBoolean(),
            issue.GetProperty("kind").GetString()));
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>(Open, TestContext.Current.CancellationToken)).GetProperty("total").GetInt32());
        var listed = Assert.Single((await ReadRunAsync(client, runId)).GetProperty("issues").EnumerateArray());
        Assert.Equal(("muted", true), (listed.GetProperty("state").GetString(), listed.GetProperty("muted").GetBoolean()));
    }

    [Fact]
    public async Task Given_a_muted_issue_When_staff_unmute_it_Then_it_is_open_again()
    {
        using var factory = new ApiFactory();
        var (runId, issueId) = await FailingSourceAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        using (await PostAsync(client, $"/admin/v1/issues/{issueId}/mute"))
        {
        }

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/unmute");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("open", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("state").GetString());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>(Open, TestContext.Current.CancellationToken)).GetProperty("total").GetInt32());
        var listed = Assert.Single((await ReadRunAsync(client, runId)).GetProperty("issues").EnumerateArray());
        Assert.Equal(("open", false), (listed.GetProperty("state").GetString(), listed.GetProperty("muted").GetBoolean()));
    }

    [Theory]
    [InlineData("mute", "mute", "muted")]
    [InlineData("unmute", null, "open")]
    public async Task Given_an_issue_already_so_When_staff_ask_again_Then_200_and_nothing_changes(string action, string? first, string state)
    {
        using var factory = new ApiFactory();
        var (_, issueId) = await FailingSourceAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        if (first is not null)
        {
            using (await PostAsync(client, $"/admin/v1/issues/{issueId}/{first}"))
            {
            }
        }

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/{action}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(state, (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("mute")]
    [InlineData("unmute")]
    public async Task Given_an_issue_that_is_over_When_staff_mute_or_unmute_it_Then_409_issue_closed_and_it_stays_closed(string action)
    {
        using var factory = new ApiFactory();
        var (runId, issueId) = await FailingSourceAsync(factory);
        factory.Time.Advance(TimeSpan.FromHours(6));
        factory.Sources.AnswerFeed(Gone, "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        await RunAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/{action}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("issue_closed", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
        var listed = Assert.Single((await ReadRunAsync(client, runId)).GetProperty("issues").EnumerateArray());
        Assert.Equal("closed", listed.GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("iss_0000000000000000000000")]
    [InlineData("not-an-id")]
    public async Task Given_no_such_issue_When_staff_mute_it_Then_404_issue_not_found(string issueId)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/mute");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("issue_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_no_staff_session_When_an_issue_is_muted_Then_401()
    {
        using var factory = new ApiFactory();
        var (_, issueId) = await FailingSourceAsync(factory);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, $"/admin/v1/issues/{issueId}/mute");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A source that is not found: one run, one open issue. Returns the run's and the issue's ids.</summary>
    private static async Task<(string RunId, string IssueId)> FailingSourceAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        factory.Sources.Answer(Gone, SourceFetchResult.Failed(FetchFailureReason.NotFound));
        var runId = await RunAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        var issue = Assert.Single((await ReadRunAsync(client, runId)).GetProperty("issues").EnumerateArray());
        return (runId, issue.GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) =>
        client.PostAsync(new Uri(path, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
}
