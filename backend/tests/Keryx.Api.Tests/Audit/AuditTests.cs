using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Audit;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Api.Tests.Audit;

// Acceptance Test
// Traces to: L2-085 AC1, AC2; L2-064; L2-065; L2-068; L2-069
// Description: Each state-changing staff action that succeeds stores one audit entry: the staff
// member's subject id, the action, the target's public id and the time, and nothing else. A
// request that fails, or that changes nothing, stores none. The trail has no endpoint: the test
// reads the store, as an operator reads the database.
public sealed class AuditTests
{
    private const string Subject = "staff-auditor";

    private static readonly string[] ArticlesRead = ["articles:read"];

    [Fact]
    public async Task Given_a_held_article_When_staff_publish_hide_and_undo_Then_each_is_audited_once_and_a_repeat_or_a_missing_article_is_not()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var id = await HeldArticleAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory, Subject);
        var now = factory.Time.GetUtcNow();

        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/articles/{id}/publish"));
        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/articles/{id}/publish"));
        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/articles/{id}/hide"));
        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/articles/{id}/revert", """{"previous_status":"published"}"""));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(staff, "/admin/v1/articles/art_aaaaaaaaaaaa/publish"));

        Assert.Equal(
            [
                (Subject, AuditAction.ArticlePublished, id, now),
                (Subject, AuditAction.ArticleHidden, id, now),
                (Subject, AuditAction.ArticleReverted, id, now),
            ],
            await EntriesAsync(factory));
    }

    [Fact]
    public async Task Given_a_source_When_staff_add_edit_remove_and_restore_it_Then_each_is_audited_and_a_refused_add_is_not()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory, Subject);
        var ct = TestContext.Current.CancellationToken;
        var source = new { kind = "rss", address = "https://lifepointfit.ca/events/feed", organization_name = "LifePoint Fitness", region = "toronto" };

        using var added = await staff.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), source, ct);
        var id = (await added.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!;
        using (var again = await staff.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), source, ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        using (var edited = await staff.PutAsJsonAsync(
            new Uri($"/admin/v1/sources/{id}", UriKind.Relative),
            new { address = "https://lifepointfit.ca/feed", organization_name = "LifePoint Fitness", region = "toronto" }, ct))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using (var removed = await staff.DeleteAsync(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), ct))
        {
            Assert.True(removed.IsSuccessStatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/sources/{id}/restore"));

        var now = factory.Time.GetUtcNow();
        Assert.Equal(
            [
                (Subject, AuditAction.SourceAdded, id, now),
                (Subject, AuditAction.SourceEdited, id, now),
                (Subject, AuditAction.SourceRemoved, id, now),
                (Subject, AuditAction.SourceRestored, id, now),
            ],
            await EntriesAsync(factory));
    }

    [Fact]
    public async Task Given_an_api_key_When_staff_create_and_revoke_it_Then_each_is_audited_by_the_client_id_and_no_entry_holds_the_key()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory, Subject);
        var ct = TestContext.Current.CancellationToken;

        using var created = await staff.PostAsJsonAsync(
            new Uri("/admin/v1/api-clients", UriKind.Relative), new { product_name = "Christian Fitness", scopes = ArticlesRead }, ct);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = body.GetProperty("client").GetProperty("id").GetString()!;
        var key = body.GetProperty("key").GetString()!;
        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/api-clients/{id}/revoke"));
        Assert.Equal(HttpStatusCode.OK, await PostAsync(staff, $"/admin/v1/api-clients/{id}/revoke"));

        var now = factory.Time.GetUtcNow();
        Assert.Equal(
            [(Subject, AuditAction.ApiKeyCreated, id, now), (Subject, AuditAction.ApiKeyRevoked, id, now)],
            await EntriesAsync(factory));
        await using var store = TestStore.Open(factory.Store);
        Assert.DoesNotContain(
            await store.AuditEntries.Select(entry => entry.SubjectId + " " + entry.TargetId).ToListAsync(ct),
            text => text.Contains(key[..12], StringComparison.Ordinal));
    }

    private static async Task<string> HeldArticleAsync(ApiFactory factory)
    {
        await using var store = TestStore.Open(factory.Store);
        var article = Article.Collect(
            new ArticleFacts("https://example.org/held", "Held", "All welcome.", "events", "toronto", [], null, 0.40m, PublicationStatus.NeedsReview),
            null, 1, "Example feed", factory.Time.GetUtcNow());
        store.Articles.Add(article);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return article.PublicId;
    }

    private static async Task<HttpStatusCode> PostAsync(HttpClient staff, string path, string body = "{}")
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await staff.PostAsync(new Uri(path, UriKind.Relative), content, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>Every entry in the order stored; each is in UTC, as the database stores it, with no changed fields.</summary>
    private static async Task<List<(string, AuditAction, string, DateTimeOffset)>> EntriesAsync(ApiFactory factory)
    {
        await using var store = TestStore.Open(factory.Store);
        var entries = await store.AuditEntries.OrderBy(entry => entry.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(entries, entry => Assert.Equal((TimeSpan.Zero, 0), (entry.OccurredAt.Offset, entry.ChangedFields.Count)));
        return [.. entries.Select(entry => (entry.SubjectId, entry.Action, entry.TargetId, entry.OccurredAt))];
    }
}
