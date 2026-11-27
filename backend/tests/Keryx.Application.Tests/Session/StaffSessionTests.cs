using Keryx.Domain.Staff;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Application.Tests.Session;

public sealed class StaffSessionTests
{
    [Theory]
    [InlineData(0, 59, false)]
    [InlineData(0, 60, true)]
    [InlineData(690, 719, false)]
    [InlineData(690, 720, true)]
    public void Given_the_last_visit_When_time_advances_Then_both_expiry_limits_apply(int lastSeenMinutes, int nowMinutes, bool expired)
    {
        var clock = new FakeTimeProvider();
        var session = new StaffSession("session", "subject", [], clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(lastSeenMinutes));
        session.Seen(clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(nowMinutes - lastSeenMinutes));
        Assert.Equal(expired, session.IsExpired(clock.GetUtcNow(), TimeSpan.FromMinutes(60), TimeSpan.FromHours(12)));
    }
}
