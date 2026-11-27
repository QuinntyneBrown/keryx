using System.Text;
using Keryx.Application.Consumer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Consumer;

/// <summary>
/// A cursor carries any sort value, and one that Keryx's own key protected but that does not hold
/// a position is refused like a forged one (L2-026).
/// </summary>
public sealed class CursorCodecTests
{
    [Fact]
    public void Given_a_position_sorted_by_a_name_with_any_characters_When_it_is_encoded_Then_it_decodes_to_the_same_position()
    {
        using var factory = new ApiFactory();
        var codec = factory.Services.GetRequiredService<ICursorCodec>();
        var position = new CursorPosition("organizations", "region=toronto&q=é", "Église Saint-Jean\n& \"Co\"", 42);

        Assert.Equal(position, codec.Decode(codec.Encode(position)));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"List\":\"articles\"}")]
    [InlineData("[1,2]")]
    public void Given_a_payload_protected_by_Keryx_that_is_not_a_position_When_it_is_decoded_Then_it_is_refused(string payload)
    {
        using var factory = new ApiFactory();
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Keryx.Consumer.Cursor.v2");
        var cursor = WebEncoders.Base64UrlEncode(protector.Protect(Encoding.UTF8.GetBytes(payload)));

        Assert.Null(factory.Services.GetRequiredService<ICursorCodec>().Decode(cursor));
    }
}
