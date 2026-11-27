using System.Security.Cryptography;

namespace Keryx.Domain;

/// <summary>
/// The ids other products and staff see (L2-009 AC4): a prefix naming the kind of record
/// (<c>src_</c>, <c>art_</c>, <c>run_</c>) and 12 random base32 characters. An id is drawn at
/// random, never from a database key, so it reveals no count, cannot be guessed from a
/// neighbour and is never freed for reuse by a delete (ADR 0007).
/// </summary>
public static class PublicIds
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";
    private const int Length = 12;

    public static string New(string prefix)
    {
        Span<char> random = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            random[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return prefix + "_" + new string(random);
    }
}
