using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Application.Errors;
using Keryx.Domain.Audit;
using Keryx.Domain.Clients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>Creates a key for a product (L2-035). Only these two fields are read from the body (L2-083).</summary>
public sealed record CreateApiClient(string? ProductName, IReadOnlyList<string>? Scopes) : IRequest<CreatedApiClientDto>;

/// <summary>
/// A product name of 1 to 80 characters once trimmed, and one or more known scopes, each once.
/// The frontend's <c>apiKeyFormValidation</c> shares this case table.
/// </summary>
internal sealed class CreateApiClientValidator : AbstractValidator<CreateApiClient>
{
    public CreateApiClientValidator()
    {
        RuleFor(request => request.ProductName)
            .Must(name => name?.Trim() is { Length: > 0 and <= ApiClient.MaxNameLength })
            .WithMessage("Enter the product's name so you can recognise its key later.");
        RuleFor(request => request.Scopes)
            .Must(scopes => scopes is { Count: > 0 } && scopes.All(ApiScopes.All.Contains) && scopes.Distinct(StringComparer.Ordinal).Count() == scopes.Count)
            .WithMessage("Choose at least one scope.");
    }
}

internal sealed class CreateApiClientHandler(IAppDbContext db, IAuditTrail audit, IApiKeyGenerator keys, TimeProvider time) : IRequestHandler<CreateApiClient, CreatedApiClientDto>
{
    public async Task<CreatedApiClientDto> Handle(CreateApiClient request, CancellationToken cancellationToken)
    {
        var name = ApiClient.Normalize(request.ProductName!);
        if (await db.ApiClients.AnyAsync(client => client.NormalizedName == name && client.RevokedAt == null, cancellationToken).ConfigureAwait(false))
        {
            throw new ClientNameTakenException();
        }

        var key = keys.NewKey();
        var created = ApiClient.Create(request.ProductName!, request.Scopes!, key, ApiKeyHash.Of(key), time.GetUtcNow());
        db.ApiClients.Add(created);
        audit.Record(AuditAction.ApiKeyCreated, created.PublicId);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another request took the name between the check and the save: the filtered unique index refused this one.
            throw new ClientNameTakenException();
        }

        return new CreatedApiClientDto(ApiClientDto.From(created), key);
    }
}

/// <summary>An active client already has this product name.</summary>
public sealed class ClientNameTakenException() : KeryxException(409, "client_name_taken", "A key with that product name already exists.");
