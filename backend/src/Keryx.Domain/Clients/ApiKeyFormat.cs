namespace Keryx.Domain.Clients;

/// <summary>
/// A well-formed key: <c>kx_live_</c> or <c>kx_test_</c>, then at least 24 ASCII letters and
/// digits. A malformed key is refused without a database read, with the same answer as an
/// unknown one (L2-033).
/// </summary>
public static class ApiKeyFormat
{
    private const int MinimumRandomLength = 24;

    /// <summary>The longest key read: a longer token is refused without hashing it.</summary>
    public const int MaxLength = 128;

    public static bool IsWellFormed(string? key)
    {
        if (key is null || key.Length > MaxLength)
        {
            return false;
        }

        var random = key.StartsWith("kx_live_", StringComparison.Ordinal) || key.StartsWith("kx_test_", StringComparison.Ordinal)
            ? key.AsSpan(8)
            : [];
        if (random.Length < MinimumRandomLength)
        {
            return false;
        }

        foreach (var character in random)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
