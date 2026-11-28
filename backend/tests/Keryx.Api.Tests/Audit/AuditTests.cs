using System.Net;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Audit;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Api.Tests.Audit;

// Acceptance Test
// Traces to: L2-085 AC1, AC2
// Description: Each state-changing staff action that succeeds stores one audit entry: the staff
// member's subject id, the action, the target's public id and the time, and nothing else. A
// request that fails, or that changes nothing, stores none. The trail has no endpoint: the test
// reads the store, as an operator reads the database.
public sealed class AuditTests
{
    private const string Subject = "staff-auditor";

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
