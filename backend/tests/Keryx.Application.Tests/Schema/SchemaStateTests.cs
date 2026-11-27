// Acceptance Test
// Traces to: L2-095
// Description: The database is ready only when it is reachable and exactly at the schema version the build expects

using Keryx.Infrastructure.Persistence.Schema;

namespace Keryx.Application.Tests.Schema;

public sealed class SchemaStateTests
{
    private const string First = "20261127000000_Initial";
    private const string Second = "20261201000000_Next";

    // One row per state a database can be in against a build.
    [Theory]
    [InlineData(true, First, First, SchemaStatus.Ready)]
    [InlineData(true, Second, Second, SchemaStatus.Ready)]
    [InlineData(true, null, null, SchemaStatus.Ready)] // a provider with no migrations
    [InlineData(true, null, First, SchemaStatus.Behind)] // never migrated
    [InlineData(true, First, Second, SchemaStatus.Behind)]
    [InlineData(true, Second, First, SchemaStatus.Ahead)]
    [InlineData(true, First, null, SchemaStatus.Ahead)] // a build with no migrations at all
    [InlineData(false, null, First, SchemaStatus.Unreachable)]
    [InlineData(false, First, First, SchemaStatus.Unreachable)] // unreachable wins over any version
    public void Given_what_the_database_holds_and_the_build_expects_Then_the_status_follows(
        bool reachable, string? applied, string? expected, SchemaStatus status)
    {
        var state = new SchemaState(reachable, applied, expected);

        Assert.Equal(status, state.Status);
        Assert.Equal(status == SchemaStatus.Ready, state.Ready);
    }

    [Fact]
    public void Given_a_database_that_did_not_answer_Then_its_state_holds_no_applied_version()
    {
        var state = SchemaState.Unreachable(First);

        Assert.Equal(new SchemaState(Reachable: false, Applied: null, Expected: First), state);
        Assert.False(state.Ready);
    }
}
