namespace Keryx.Application.Clients;

/// <summary>Draws a new API key: a prefix naming the environment and random characters from a cryptographic generator (L2-035).</summary>
public interface IApiKeyGenerator
{
    string NewKey();
}

/// <summary>The SHA-256 hash of a key, lower-case hex: all Keryx keeps of it.</summary>
public static class ApiKeyHash
{
    public static string Of(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>Whether a presented key hashes to a stored hash, compared in constant time (L2-033 AC4).</summary>
    public static bool Matches(string key, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(storedHash);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(Of(key)), System.Text.Encoding.ASCII.GetBytes(storedHash));
    }
}
