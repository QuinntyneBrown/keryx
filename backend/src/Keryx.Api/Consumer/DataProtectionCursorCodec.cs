using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Keryx.Application.Consumer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Keryx.Api.Consumer;

/// <summary>
/// A cursor is its position protected by ASP.NET Core Data Protection and written URL-safe: a
/// product cannot read or forge one. It does not expire; it stops decoding only when its
/// protection key is retired.
/// </summary>
internal sealed class DataProtectionCursorCodec(IDataProtectionProvider provider) : ICursorCodec
{
    private const char Separator = '\n';
    private readonly IDataProtector protector = provider.CreateProtector("Keryx.Consumer.Cursor.v1");

    public string Encode(CursorPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        var text = string.Join(Separator, position.List, position.Filters,
            position.SortedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), position.Id.ToString(CultureInfo.InvariantCulture));
        return WebEncoders.Base64UrlEncode(protector.Protect(Encoding.UTF8.GetBytes(text)));
    }

    public CursorPosition? Decode(string cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        try
        {
            var parts = Encoding.UTF8.GetString(protector.Unprotect(WebEncoders.Base64UrlDecode(cursor))).Split(Separator);
            return parts.Length == 4
                && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? new CursorPosition(parts[0], parts[1], new DateTimeOffset(ticks, TimeSpan.Zero), id)
                : null;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
