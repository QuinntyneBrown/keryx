using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Keryx.Api.Tests.Session;

namespace Keryx.Api.Tests.AdminSettings;

// Acceptance Test
// Traces to: L2-070 AC1, AC5; L2-071 AC7; L2-005 AC3; L2-018
// Description: Staff read every setting with its version, and save changes with the version they
// loaded. A save takes effect at once (the schedule reads the new interval), adds a region for a
// new focus area, and returns a new version. A save made from an older version, or one with a
// value out of range, changes nothing.
public sealed class SettingsTests
{
    private static readonly Uri Settings = new("/admin/v1/settings", UriKind.Relative);

    private static readonly string[] Fields =
    [
        "alert_email", "alert_failure_streak", "alert_on_run_failure", "daily_digest", "exclusions",
        "focus_areas", "include_national", "include_ontario_wide", "interval_hours", "merge_duplicates",
        "publish_threshold", "retention_months", "timeout_seconds", "topics", "version",
    ];

    [Fact]
    public async Task Given_the_seeded_settings_When_staff_read_them_Then_every_value_and_a_version_are_served_and_never_cached()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await client.GetAsync(Settings, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(Fields, body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal((6, 30, 0.60m, 24, 3), (
            body.GetProperty("interval_hours").GetInt32(),
            body.GetProperty("timeout_seconds").GetInt32(),
            body.GetProperty("publish_threshold").GetDecimal(),
            body.GetProperty("retention_months").GetInt32(),
            body.GetProperty("alert_failure_streak").GetInt32()));
        Assert.Equal(["casino", "supplement ads", "sponsored"], body.GetProperty("exclusions").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("alert_email").ValueKind);
        Assert.Equal((true, false, true, true, true), (
            body.GetProperty("include_ontario_wide").GetBoolean(),
            body.GetProperty("include_national").GetBoolean(),
            body.GetProperty("merge_duplicates").GetBoolean(),
            body.GetProperty("alert_on_run_failure").GetBoolean(),
            body.GetProperty("daily_digest").GetBoolean()));
        Assert.Equal(JsonValueKind.String, body.GetProperty("version").ValueKind);
    }

    [Fact]
    public async Task Given_no_staff_session_When_settings_are_read_Then_401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Settings, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Given_loaded_settings_When_staff_save_changes_Then_200_with_them_and_a_new_version_and_they_take_effect()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var loaded = await ReadAsync(client);
        var changed = Changed(loaded, settings =>
        {
            settings["interval_hours"] = 3;
            settings["timeout_seconds"] = 45;
            settings["publish_threshold"] = 0.65m;
            settings["topics"] = new JsonArray("fitness", "Pickleball");
            settings["focus_areas"] = new JsonArray("Toronto", "Hamilton", "Richmond-Hill");
            settings["alert_email"] = "ops@keryx.ca";
            settings["daily_digest"] = false;
        });

        using var response = await client.PutAsJsonAsync(Settings, changed, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.NotEqual(loaded.GetProperty("version").GetString(), saved.GetProperty("version").GetString());
        Assert.Equal((3, 45, 0.65m, "ops@keryx.ca", false), (
            saved.GetProperty("interval_hours").GetInt32(),
            saved.GetProperty("timeout_seconds").GetInt32(),
            saved.GetProperty("publish_threshold").GetDecimal(),
            saved.GetProperty("alert_email").GetString(),
            saved.GetProperty("daily_digest").GetBoolean()));
        Assert.Equal(["fitness", "Pickleball"], saved.GetProperty("topics").EnumerateArray().Select(value => value.GetString()));
        var read = await ReadAsync(client);
        Assert.Equal(saved.GetRawText(), read.GetRawText());

        // The schedule reads the new interval at its next decision (L2-005 AC3).
        var schedule = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/overview/schedule", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(3, schedule.GetProperty("interval_hours").GetInt32());

        // A new focus area becomes a region in Toronto's zone; one that is already a region, by name or by
        // slug, does not repeat (L2-018).
        var regions = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/regions", UriKind.Relative), TestContext.Current.CancellationToken);
        var items = regions.GetProperty("items").EnumerateArray().ToList();
        var hamilton = Assert.Single(items, region => region.GetProperty("name").GetString() == "Hamilton");
        Assert.Equal(("hamilton", "America/Toronto"), (hamilton.GetProperty("slug").GetString(), hamilton.GetProperty("time_zone").GetString()));
        Assert.Single(items, region => region.GetProperty("slug").GetString() == "richmond-hill");
        Assert.Single(items, region => region.GetProperty("name").GetString() == "Toronto");
    }

    [Fact]
    public async Task Given_settings_saved_elsewhere_since_they_loaded_When_staff_save_Then_409_settings_changed_and_nothing_is_overwritten()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var loaded = await ReadAsync(client);
        using var first = await client.PutAsJsonAsync(Settings, Changed(loaded, settings => settings["timeout_seconds"] = 60), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await client.PutAsJsonAsync(Settings, Changed(loaded, settings => settings["timeout_seconds"] = 90), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("settings_changed", problem.GetProperty("code").GetString());
        Assert.Equal(60, (await ReadAsync(client)).GetProperty("timeout_seconds").GetInt32());
    }

    [Theory]
    [InlineData("interval_hours", "2")]
    [InlineData("timeout_seconds", "4")]
    [InlineData("timeout_seconds", "121")]
    [InlineData("publish_threshold", "0.62")]
    [InlineData("publish_threshold", "0.95")]
    [InlineData("topics", "[]")]
    [InlineData("topics", "[\"fitness\",\" FITNESS \"]")]
    [InlineData("topics", "[\"x\"]")]
    [InlineData("topics", "[\" a \"]")]
    [InlineData("exclusions", "[\"a value that is far too long to be one topic\"]")]
    [InlineData("focus_areas", "[]")]
    [InlineData("retention_months", "0")]
    [InlineData("retention_months", "121")]
    [InlineData("alert_email", "\"ops@keryx\"")]
    [InlineData("alert_email", "\"ops@kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk.ca\"")]
    [InlineData("alert_failure_streak", "21")]
    public async Task Given_a_value_out_of_range_When_staff_save_Then_400_validation_failed_names_the_field_and_nothing_changes(string field, string value)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var loaded = await ReadAsync(client);

        using var response = await client.PutAsJsonAsync(Settings, Changed(loaded, settings => settings[field] = JsonNode.Parse(value)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
        Assert.Equal(loaded.GetRawText(), (await ReadAsync(client)).GetRawText());
    }

    [Fact]
    public async Task Given_fifty_topics_of_forty_characters_and_no_alert_email_When_staff_save_Then_they_are_within_range()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var loaded = await ReadAsync(client);
        var topics = new JsonArray([.. Enumerable.Range(0, 50).Select(n => JsonValue.Create($"{n:00}{new string('t', 38)}"))]);

        using var response = await client.PutAsJsonAsync(Settings, Changed(loaded, settings =>
        {
            settings["topics"] = topics;
            settings["exclusions"] = new JsonArray();
            settings["interval_hours"] = 24;
            settings["timeout_seconds"] = 5;
            settings["publish_threshold"] = 0.30m;
            settings["retention_months"] = 120;
            settings["alert_failure_streak"] = 20;
            settings["alert_email"] = null;
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>(Settings, TestContext.Current.CancellationToken);

    private static JsonObject Changed(JsonElement loaded, Action<JsonObject> change)
    {
        var settings = JsonNode.Parse(loaded.GetRawText())!.AsObject();
        change(settings);
        return settings;
    }
}
