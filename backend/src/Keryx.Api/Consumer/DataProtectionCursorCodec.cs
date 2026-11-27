using System.Security.Cryptography;
using System.Text.Json;
using Keryx.Application.Consumer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Keryx.Api.Consumer;

/// <summary>
/// A cursor is its position as JSON, protected by ASP.NET Core Data Protection and written
/// URL-safe: a product cannot read or forge one, and a sort value such as a name may hold any
/// character. It does not expire; it stops decoding only when its protection key is retired.
/// </summary>
internal sealed class DataProtectionCursorCodec(IDataProtectionProvider provider) : ICursorCodec
{
    private readonly IDataProtector protector = provider.CreateProtector("Keryx.Consumer.Cursor.v2");

    public string Encode(CursorPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return WebEncoders.Base64UrlEncode(protector.Protect(JsonSerializer.SerializeToUtf8Bytes(position)));
    }

    public CursorPosition? Decode(string cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        try
        {
            var position = JsonSerializer.Deserialize<CursorPosition>(protector.Unprotect(WebEncoders.Base64UrlDecode(cursor)));
            return position is { List: not null, Filters: not null, Sort: not null } ? position : null;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}
