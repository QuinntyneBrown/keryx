using Keryx.Domain.Clients;

namespace Keryx.Application.Tests.Clients;

/// <summary>One caller's fixed window of a minute (L2-038): one row per case.</summary>
public sealed class RateWindowTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    // Requests already taken in the window, seconds since it opened, then the decision:
    // allowed, remaining, seconds until it resets, and Retry-After (0 when allowed).
    [Theory]
    [InlineData(0, 0.0, true, 2, 60, 0)]
    [InlineData(1, 10.0, true, 1, 50, 0)]
    [InlineData(2, 10.0, true, 0, 50, 0)]
    [InlineData(3, 10.0, false, 0, 50, 50)]
    [InlineData(3, 59.5, false, 0, 1, 1)]
    [InlineData(3, 60.0, true, 2, 60, 0)]
    [InlineData(3, 75.0, true, 2, 60, 0)]
    public void Given_a_window_with_a_limit_of_3_When_a_request_is_taken_Then_it_decides_as_the_row_says(
        int taken, double secondsLater, bool allowed, int remaining, int resetSeconds, int retryAfterSeconds)
    {
        var window = RateWindow.Open(Start, limit: 3);
        for (var i = 0; i < taken; i++)
        {
            Assert.True(window.TryTake(Start).Allowed);
        }

        var decision = window.TryTake(Start.AddSeconds(secondsLater));

        Assert.Equal((allowed, 3, remaining, resetSeconds, retryAfterSeconds),
            (decision.Allowed, decision.Limit, decision.Remaining, decision.ResetSeconds, decision.RetryAfterSeconds));
    }

    [Fact]
    public void Given_a_full_window_When_it_is_asked_whether_it_is_full_Then_it_says_so_without_taking_and_not_after_it_passes()
    {
        var window = RateWindow.Open(Start, limit: 1);
        Assert.False(window.IsFull(Start));
        Assert.True(window.TryTake(Start).Allowed);

        Assert.True(window.IsFull(Start.AddSeconds(59)));
        Assert.True(window.IsFull(Start.AddSeconds(59)));
        Assert.False(window.IsFull(Start.AddSeconds(60)));
    }

    [Fact]
    public void Given_a_window_When_it_has_passed_Then_it_says_it_can_be_evicted()
    {
        var window = RateWindow.Open(Start, limit: 1);

        Assert.False(window.HasPassed(Start.AddSeconds(59)));
        Assert.True(window.HasPassed(Start.AddSeconds(60)));
    }
}
