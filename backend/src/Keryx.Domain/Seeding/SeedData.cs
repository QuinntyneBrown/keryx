using Keryx.Domain.Reference;
using Keryx.Domain.Sources;

namespace Keryx.Domain.Seeding;

/// <summary>
/// The rows every deployment needs (L2-095): the regions, the categories, the default settings
/// and the seeded news query sources. Each call returns new instances, so a caller may add them
/// to a unit of work.
/// </summary>
public static class SeedData
{
    private const string Toronto = "America/Toronto";

    /// <summary>
    /// Toronto first of all, then the GTA municipalities, then the wider areas (L2-018). The
    /// municipalities rank 10 to 100 and the wider areas from 1000, so a focus area staff add
    /// later ranks between them.
    /// </summary>
    public static IReadOnlyList<Region> Regions() =>
    [
        new("toronto", "Toronto", Toronto, 10),
        new("scarborough", "Scarborough", Toronto, 20),
        new("mississauga", "Mississauga", Toronto, 30),
        new("brampton", "Brampton", Toronto, 40),
        new("markham", "Markham", Toronto, 50),
        new("vaughan", "Vaughan", Toronto, 60),
        new("oakville", "Oakville", Toronto, 70),
        new("richmond-hill", "Richmond Hill", Toronto, 80),
        new("pickering", "Pickering", Toronto, 90),
        new("ajax", "Ajax", Toronto, 100),
        new("gta", "Greater Toronto Area", Toronto, 1000),
        new("ontario", "Ontario", Toronto, 1100),
        new("canada", "Canada", Toronto, 1200),
        new("global", "Global", "Etc/UTC", 1300),
    ];

    /// <summary>The six categories, in the order of L2-015.</summary>
    public static IReadOnlyList<Category> Categories() =>
    [
        new("events", "Events", 1),
        new("races-challenges", "Races & Challenges", 2),
        new("gyms-studios", "Gyms & Studios", 3),
        new("ministry-programs", "Ministry Programs", 4),
        new("nutrition", "Nutrition", 5),
        new("faith-wellness", "Faith & Wellness", 6),
    ];

    /// <summary>The settings a new deployment starts with: the values the mocks show.</summary>
    public static Settings DefaultSettings() => Settings.Create(DefaultValues());

    /// <summary>The default values, which a test may change one by one with <c>with</c>.</summary>
    public static SettingsValues DefaultValues() => new(
        IntervalHours: 6,
        TimeoutSeconds: 30,
        PublishThreshold: 0.60m,
        Topics: ["fitness", "running", "strength", "sports ministry", "wellness", "church", "faith", "nutrition"],
        Exclusions: ["casino", "supplement ads", "sponsored"],
        FocusAreas: ["Toronto", "Mississauga", "Brampton", "Markham", "Vaughan", "Oakville", "Richmond Hill", "Pickering", "Ajax"],
        IncludeOntarioWide: true,
        IncludeNational: false,
        MergeDuplicates: true,
        RetentionMonths: 24,
        AlertEmail: null,
        AlertFailureStreak: 3,
        AlertOnRunFailure: true,
        DailyDigest: true);

    /// <summary>The news query sources; staff cannot add this kind (L2-001).</summary>
    public static IReadOnlyList<Source> NewsQuerySources(DateTimeOffset now) =>
    [
        Source.SeededNewsQuery(
            seedKey: "toronto-church-fitness",
            organizationName: "Toronto church fitness",
            query: "\"church\" AND (fitness OR run) near:Toronto",
            regionSlug: "gta",
            now),
    ];
}
