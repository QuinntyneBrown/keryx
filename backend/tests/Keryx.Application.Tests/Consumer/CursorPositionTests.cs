using Keryx.Application.Consumer;

namespace Keryx.Application.Tests.Consumer;

/// <summary>The time a cursor's sort value holds in a time-sorted list: one row per case.</summary>
public sealed class CursorPositionTests
{
    [Theory]
    [InlineData("639000000000000000", true)]
    [InlineData("0", true)]
    [InlineData("3155378975999999999", true)]
    [InlineData("3155378976000000000", false)]
    [InlineData("9223372036854775807", false)]
    [InlineData("-1", false)]
    [InlineData("Redeemed Run Club", false)]
    [InlineData("", false)]
    public void Given_a_sort_value_When_its_time_is_read_Then_it_is_a_time_only_when_it_is_ticks_a_time_can_hold(string sort, bool isTime)
    {
        var position = new CursorPosition("articles", string.Empty, sort, 1);

        Assert.Equal(isTime, position.Time is not null);
    }

    [Fact]
    public void Given_a_time_When_a_position_is_made_at_it_Then_its_time_is_the_same_instant_in_UTC()
    {
        var at = new DateTimeOffset(2026, 11, 26, 7, 0, 0, TimeSpan.FromHours(-5));

        var position = CursorPosition.At("articles", string.Empty, at, 7);

        Assert.Equal((at, TimeSpan.Zero), (position.Time!.Value, position.Time.Value.Offset));
    }
}
