using FluentValidation;
using Keryx.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>The rules a new source must meet before it is stored (L2-001 AC2, L2-083 AC1).</summary>
internal sealed class CreateSourceValidator : AbstractValidator<CreateSource>
{
    public CreateSourceValidator(IAppDbContext db)
    {
        RuleFor(request => request.Kind)
            .Must(kind => SourceWords.KindOf(kind) is not null)
            .WithMessage("Choose a kind: rss, website, instagram or eventbrite.");
        RuleFor(request => request.Address)
            .Must(SourceAddress.IsAcceptable)
            .WithMessage("Enter a full URL starting with http:// or https://, with no user name, password or port.");
        RuleFor(request => request.OrganizationName)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Length <= 120)
            .WithMessage("Enter the organization's name, up to 120 characters.");
        RuleFor(request => request.Region)
            .MustAsync(async (slug, cancellationToken) =>
                slug is not null && await db.Regions.AnyAsync(region => region.Slug == slug, cancellationToken).ConfigureAwait(false))
            .WithMessage("Choose a region from the list.");
    }
}
