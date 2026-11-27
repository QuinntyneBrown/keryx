using Keryx.Infrastructure.Clients;

namespace Keryx.Application.Tests.Clients;

/// <summary>The rate windows hold only the callers of the last minute, so memory does not grow with every address ever seen (L2-038).</summary>
public sealed class RateWindowStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_callers_whose_windows_have_passed_When_a_request_comes_a_minute_later_Then_only_its_caller_is_held()
    {
        var store = new InMemoryRateWindowStore();
        for (var i = 0; i < 100; i++)
        {
            store.TryTake($"address:10.0.0.{i}", Start, limit: 60);
        }

        Assert.Equal(100, store.Callers);
        store.TryTake("address:10.0.1.1", Start.AddSeconds(30), limit: 60);
        Assert.Equal(101, store.Callers);

        store.TryTake("address:10.0.1.2", Start.AddMinutes(1).AddSeconds(30), limit: 60);

        Assert.Equal(1, store.Callers);
    }
}
