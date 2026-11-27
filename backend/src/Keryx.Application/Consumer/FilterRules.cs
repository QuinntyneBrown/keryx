using FluentValidation;
using Keryx.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>The filter rules every /v1 list shares, each refused with <c>invalid_filter</c> (L2-026).</summary>
internal static class FilterRules
{
    public const string InvalidFilter = "invalid_filter";

    /// <summary>A region slug that /v1/regions lists.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeKnownRegion<T>(this IRuleBuilder<T, string?> rule, IAppDbContext db) =>
        rule.MustAsync(async (region, cancellationToken) => region is null
                || await db.Regions.AnyAsync(known => known.Slug == region, cancellationToken).ConfigureAwait(false))
            .WithErrorCode(InvalidFilter)
            .WithMessage("Give region as a slug that /v1/regions lists.");

    /// <summary>The shape of an organization's id: <c>org_</c> and 12 base32 characters.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeOrganizationId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(org => org is null || Keryx.Domain.PublicIds.IsWellFormed(org, "org"))
            .WithErrorCode(InvalidFilter)
            .WithMessage("Give org as an organization's id, org_ followed by its characters.");
}
