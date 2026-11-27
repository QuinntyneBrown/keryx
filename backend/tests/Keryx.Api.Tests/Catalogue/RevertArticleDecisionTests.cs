using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Catalogue.DecideOnArticleTests;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-014 AC4, AC6
// Description: Staff undo a decision in the session that made it: the article returns to the status
// it held and to whoever had set it. Another session, a status it did not hold, or no decision to
// undo is revert_unavailable; a word that is no status is invalid_status.
public sealed class RevertArticleDecisionTests
{
    [Fact]
    public async Task Given_staff_published_a_held_article_When_they_revert_it_in_that_session_Then_it_is_held_by_the_system_again_and_not_served()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        using var product = Client(factory, await KeyAsync(factory, "articles:read"));
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(staff, id, "publish"))
        {
        }

        using var response = await RevertAsync(staff, id, "needs_review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((id, "needs_review", "system", null), Decision(body));
        var read = await staff.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(("needs_review", "below_publish_threshold", "system"),
            (read.GetProperty("status").GetString(), read.GetProperty("status_reason").GetString(), read.GetProperty("decided_by").GetString()));
        using var served = await product.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
    }

    [Fact]
    public async Task Given_staff_hid_a_published_article_When_they_revert_it_Then_it_is_served_again()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        using var product = Client(factory, await KeyAsync(factory, "articles:read"));
        var id = await ArticleIdAsync(factory, Published.Link);
        using (await DecideAsync(staff, id, "hide"))
        {
        }

        using var response = await RevertAsync(staff, id, "published");

        Assert.Equal((id, "published", "system", null), Decision(await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)));
        using var served = await product.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task Given_staff_decided_twice_When_they_revert_Then_the_article_returns_to_the_status_and_decider_of_the_first_decision()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(staff, id, "hide"))
        {
        }

        using (await DecideAsync(staff, id, "publish"))
        {
        }

        using var response = await RevertAsync(staff, id, "hidden");

        Assert.Equal((id, "hidden", "staff", null), Decision(await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Given_a_decision_made_in_another_session_When_it_is_reverted_Then_409_revert_unavailable_and_nothing_changes()
    {
        using var factory = await CollectedAsync();
        using var earlier = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(earlier, id, "publish"))
        {
        }

        using var later = await StaffClient.OpenAsync(factory);
        using var response = await RevertAsync(later, id, "needs_review");

        await AssertUnavailableAsync(response);
        await AssertStatusAsync(later, id, "published");
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("published")]
    public async Task Given_a_decision_When_it_is_reverted_to_a_status_the_article_did_not_hold_Then_409_revert_unavailable(string status)
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(staff, id, "publish"))
        {
        }

        using var response = await RevertAsync(staff, id, status);

        await AssertUnavailableAsync(response);
        await AssertStatusAsync(staff, id, "published");
    }

    [Fact]
    public async Task Given_a_reverted_decision_When_it_is_reverted_again_Then_409_revert_unavailable()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(staff, id, "publish"))
        {
        }

        using (await RevertAsync(staff, id, "needs_review"))
        {
        }

        using var again = await RevertAsync(staff, id, "needs_review");

        await AssertUnavailableAsync(again);
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Given_a_word_that_is_no_status_When_it_is_sent_Then_400_invalid_status(string? status)
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);

        using var response = await RevertAsync(staff, id, status);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("invalid_status", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_an_unknown_id_When_a_revert_is_sent_Then_404_article_not_found()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await RevertAsync(staff, "art_doesnotexist000000", "needs_review");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("article_not_found", problem.GetProperty("code").GetString());
    }

    private static Task<HttpResponseMessage> RevertAsync(HttpClient staff, string id, string? previousStatus) =>
        DecideAsync(staff, id, "revert", new { previous_status = previousStatus });

    private static async Task AssertUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("revert_unavailable", problem.GetProperty("code").GetString());
    }

    private static async Task AssertStatusAsync(HttpClient staff, string id, string status)
    {
        var read = await staff.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal((status, "staff"), (read.GetProperty("status").GetString(), read.GetProperty("decided_by").GetString()));
    }
}
