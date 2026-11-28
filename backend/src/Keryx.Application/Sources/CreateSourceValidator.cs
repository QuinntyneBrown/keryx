using FluentValidation;
using Keryx.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>The rules a new source must meet before it is stored (L2-001 AC2, L2-083 AC1).</summary>
internal sealed class CreateSourceValidator : AbstractValidator<CreateSource>
{
    public CreateSourceValidator(IAppDbContext db, SafeAddressCheck safety)
    {
        RuleFor(request => request.Kind)
            .Must(kind => SourceWords.KindOf(kind) is not null)
            .WithMessage("Choose a kind: rss, website, instagram or eventbrite.");
        RuleFor(request => request.Address).SafeUrl(safety);
        RuleFor(request => request.OrganizationName).OrganizationName();
        RuleFor(request => request.Region).KnownRegion(db);
    }
}

/// <summary>The rules for a source's fields, shared by adding and editing one (L2-001 AC2, L2-003 AC1).</summary>
internal static class SourceFieldRules
{
    public static void SafeUrl<T>(this IRuleBuilderInitial<T, string?> rule, SafeAddressCheck safety) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(SourceAddress.IsAcceptable)
            .WithMessage("Enter a full URL starting with http:// or https://, with no user name, password or port.")
            .MustAsync((address, cancellationToken) => safety.IsSafeAsync(new Uri(address!), cancellationToken))
            .WithErrorCode("unsafe_address")
            .WithMessage("Keryx can't fetch this address: it isn't a public site.");

    public static void OrganizationName<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(name => !string.IsNullOrWhiteSpace(name) && name.Length <= 120)
            .WithMessage("Enter the organization's name, up to 120 characters.");

    public static void KnownRegion<T>(this IRuleBuilderInitial<T, string?> rule, IAppDbContext db) =>
        rule.MustAsync(async (slug, cancellationToken) =>
                slug is not null && await db.Regions.AnyAsync(region => region.Slug == slug, cancellationToken).ConfigureAwait(false))
            .WithMessage("Choose a region from the list.");
}
