using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Application.Errors;
using Keryx.Domain;
using Keryx.Domain.Audit;
using Keryx.Domain.Reference;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.AdminSettings;

/// <summary>Staff's settings, saved from the version they were loaded at (L2-071 AC4, AC7).</summary>
public sealed record UpdateSettings(
    int IntervalHours,
    int TimeoutSeconds,
    decimal PublishThreshold,
    IReadOnlyList<string>? Topics,
    IReadOnlyList<string>? Exclusions,
    IReadOnlyList<string>? FocusAreas,
    bool IncludeOntarioWide,
    bool IncludeNational,
    bool MergeDuplicates,
    int RetentionMonths,
    string? AlertEmail,
    int AlertFailureStreak,
    bool AlertOnRunFailure,
    bool DailyDigest,
    string? Version) : IRequest<SettingsDto>;

/// <summary>The settings were saved elsewhere since this reading; nothing is overwritten (L2-071 AC7).</summary>
public sealed class SettingsChangedException()
    : KeryxException(409, "settings_changed", "The settings were changed elsewhere since they were loaded.");

/// <summary>The ranges of L2-070 AC5, one entry per field that breaks them.</summary>
internal sealed partial class UpdateSettingsValidator : AbstractValidator<UpdateSettings>
{
    private static readonly int[] Intervals = [1, 3, 6, 12, 24];

    public UpdateSettingsValidator()
    {
        RuleFor(request => request.IntervalHours).Must(hours => Intervals.Contains(hours))
            .WithMessage("Choose 1, 3, 6, 12 or 24 hours.");
        RuleFor(request => request.TimeoutSeconds).InclusiveBetween(5, 120)
            .WithMessage("Enter a timeout between 5 and 120 seconds.");
        RuleFor(request => request.PublishThreshold)
            .Must(threshold => threshold is >= 0.30m and <= 0.90m && threshold * 100 % 5 == 0)
            .WithMessage("Choose a threshold from 0.30 to 0.90 in steps of 0.05.");
        RuleFor(request => request.Topics).Must(values => IsList(values, 1)).WithMessage(ListMessage(1));
        RuleFor(request => request.Exclusions).Must(values => IsList(values, 0)).WithMessage(ListMessage(0));
        RuleFor(request => request.FocusAreas).Must(values => IsList(values, 1)).WithMessage(ListMessage(1));
        RuleFor(request => request.RetentionMonths).InclusiveBetween(1, 120)
            .WithMessage("Enter a period between 1 and 120 months.");
        RuleFor(request => request.AlertEmail)
            .Must(email => email is null || (email.Length <= 254 && Address().IsMatch(email)))
            .WithMessage("Enter an address like name@example.com.");
        RuleFor(request => request.AlertFailureStreak).InclusiveBetween(1, 20)
            .WithMessage("Enter a number of runs between 1 and 20.");
    }

    /// <summary>Between the least and 50 values, each 2 to 40 characters and unique ignoring case and surrounding space.</summary>
    private static bool IsList(IReadOnlyList<string>? values, int least) =>
        values is not null
        && values.Count >= least && values.Count <= 50
        && values.All(value => value is not null && value.Trim().Length is >= 2 and <= 40)
        && values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count;

    private static string ListMessage(int least) =>
        string.Create(CultureInfo.InvariantCulture, $"Keep {least} to 50 different values of 2 to 40 characters.");

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Address();
}

/// <summary>
/// Saves the settings unless they changed since the client's reading (L2-071 AC7). A focus area
/// that is not yet a region becomes one in Toronto's zone, after the places already in focus
/// (L2-018). The schedule reads the new interval at its next decision (L2-005 AC3).
/// </summary>
internal sealed class UpdateSettingsHandler(IAppDbContext db, IAuditTrail audit) : IRequestHandler<UpdateSettings, SettingsDto>
{
    // Regions ranked below this are the places in focus; the wider areas come after.
    private const int WiderAreas = 1000;

    public async Task<SettingsDto> Handle(UpdateSettings request, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleAsync(cancellationToken).ConfigureAwait(false);
        if (request.Version != settings.Version.ToString(CultureInfo.InvariantCulture))
        {
            throw new SettingsChangedException();
        }

        var focusAreas = Trimmed(request.FocusAreas!);
        var submitted = new SettingsValues(
            request.IntervalHours,
            request.TimeoutSeconds,
            request.PublishThreshold,
            Trimmed(request.Topics!),
            Trimmed(request.Exclusions!),
            focusAreas,
            request.IncludeOntarioWide,
            request.IncludeNational,
            request.MergeDuplicates,
            request.RetentionMonths,
            request.AlertEmail,
            request.AlertFailureStreak,
            request.AlertOnRunFailure,
            request.DailyDigest);

        // The names of what changed, never the values: the alert address stays out (L2-085 AC2).
        audit.Record(AuditAction.SettingsSaved, "settings", SettingsDiff.ChangedFields(settings.Values(), submitted));
        settings.Update(submitted);
        await AddRegionsAsync(focusAreas, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SettingsDto.From(settings);
    }

    private async Task AddRegionsAsync(IReadOnlyList<string> focusAreas, CancellationToken cancellationToken)
    {
        var regions = await db.Regions.ToListAsync(cancellationToken).ConfigureAwait(false);
        var rank = regions.Where(region => region.Rank < WiderAreas).Select(region => region.Rank).DefaultIfEmpty(0).Max();
        foreach (var area in focusAreas)
        {
            var slug = Slug(area);
            if (slug.Length == 0 || regions.Any(region => region.Slug == slug || string.Equals(region.Name, area, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            rank += 10;
            var region = new Region(slug, area, TorontoTime.ZoneId, rank);
            regions.Add(region);
            db.Regions.Add(region);
        }
    }

    private static string[] Trimmed(IReadOnlyList<string> values) => [.. values.Select(value => value.Trim())];

    /// <summary>Lowercase letters and digits, the rest joined by single hyphens (<c>richmond-hill</c>).</summary>
    private static string Slug(string name)
    {
        var words = Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0);
        return string.Join('-', words);
    }
}
