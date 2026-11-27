using Keryx.Application.Clients;
using Keryx.Domain.Clients;

namespace Keryx.Application.Tests.Clients;

/// <summary>The rules a presented key meets before and when it is compared (L2-033 AC1, AC4).</summary>
public sealed class ApiKeyRulesTests
{
    private const string Random24 = "R8vN2qLm0aXe5TbW9yKc3HdF";

    [Theory]
    [InlineData("kx_live_" + Random24, true)]
    [InlineData("kx_test_" + Random24, true)]
    [InlineData("kx_live_" + Random24 + "Z", true)]
    [InlineData("kx_live_R8vN2qLm0aXe5TbW9yKc3Hd", false)]
    [InlineData("kx_prod_" + Random24, false)]
    [InlineData("KX_LIVE_" + Random24, false)]
    [InlineData("kx_live_R8vN2qLm0aXe5TbW9yKc3Hd-", false)]
    [InlineData("kx_live_R8vN2qLm0aXe5TbW9yKc3Hd_", false)]
    [InlineData("kx_live_R8vN2qLm0aXe5TbW9yKc3Hdé", false)]
    [InlineData(" kx_live_" + Random24, false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Given_a_presented_key_When_its_shape_is_checked_Then_only_a_known_prefix_and_24_letters_or_digits_pass(string? key, bool wellFormed) =>
        Assert.Equal(wellFormed, ApiKeyFormat.IsWellFormed(key));

    [Fact]
    public void Given_a_key_of_128_characters_When_its_shape_is_checked_Then_it_passes_and_129_does_not()
    {
        Assert.True(ApiKeyFormat.IsWellFormed("kx_live_" + new string('a', 120)));
        Assert.False(ApiKeyFormat.IsWellFormed("kx_live_" + new string('a', 121)));
    }

    [Fact]
    public void Given_a_stored_hash_When_keys_are_matched_Then_only_the_key_that_made_it_matches()
    {
        var key = "kx_live_" + Random24;
        var stored = ApiKeyHash.Of(key);

        Assert.Equal(64, stored.Length);
        Assert.True(ApiKeyHash.Matches(key, stored));
        Assert.False(ApiKeyHash.Matches(key[..^1] + "G", stored));
        Assert.False(ApiKeyHash.Matches(key, stored.ToUpperInvariant()));
        Assert.False(ApiKeyHash.Matches(key, stored[..^1]));
    }
}
