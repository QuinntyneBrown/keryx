// Acceptance Test
// Traces to: L2-083
// Description: Every request is validated before its handler, and a refusal names each field and its code

using FluentValidation;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private static ServiceProvider Services(Counter counter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ValidationBehaviorTests).Assembly));
        services.AddValidatorsFromAssembly(typeof(ValidationBehaviorTests).Assembly, includeInternalTypes: true);
        services.AddSingleton(counter);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Given_a_valid_request_When_it_is_sent_Then_its_handler_runs_once()
    {
        var counter = new Counter();
        await using var services = Services(counter);

        var reply = await services.GetRequiredService<ISender>().Send(new Rename("Keryx", 3), TestContext.Current.CancellationToken);

        Assert.Equal("Keryx", reply);
        Assert.Equal(1, counter.Handled);
    }

    // One row per rule of the request's validator: the field, its message, and the refusal's code.
    [Theory]
    [InlineData("", 3, "Name", "'Name' must not be empty.", "validation_failed")]
    [InlineData("Keryx", 0, "Limit", "'Limit' must be between 1 and 100. You entered 0.", "invalid_limit")]
    [InlineData("Keryx", 101, "Limit", "'Limit' must be between 1 and 100. You entered 101.", "invalid_limit")]
    public async Task Given_a_request_that_breaks_one_rule_When_it_is_sent_Then_it_is_refused_before_its_handler(
        string name, int limit, string field, string message, string code)
    {
        var counter = new Counter();
        await using var services = Services(counter);

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(
            () => services.GetRequiredService<ISender>().Send(new Rename(name, limit), TestContext.Current.CancellationToken));

        Assert.Equal(400, refusal.Status);
        Assert.Equal(code, refusal.Code);
        Assert.Equal([field], refusal.Errors.Keys);
        Assert.Equal([message], refusal.Errors[field]);
        Assert.Equal(0, counter.Handled);
    }

    [Fact]
    public async Task Given_a_request_that_breaks_two_rules_When_it_is_sent_Then_each_field_has_its_entry()
    {
        var counter = new Counter();
        await using var services = Services(counter);

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(
            () => services.GetRequiredService<ISender>().Send(new Rename("", 0), TestContext.Current.CancellationToken));

        Assert.Equal(["Limit", "Name"], refusal.Errors.Keys.Order(StringComparer.Ordinal));
        // The code a rule set is the one a client branches on, wherever that rule comes.
        Assert.Equal("invalid_limit", refusal.Code);
        Assert.Equal(0, counter.Handled);
    }
}

public sealed class Counter
{
    public int Handled { get; set; }
}

public sealed record Rename(string Name, int Limit) : IRequest<string>;

internal sealed class RenameValidator : AbstractValidator<Rename>
{
    public RenameValidator()
    {
        RuleFor(request => request.Name).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 100).WithErrorCode("invalid_limit");
    }
}

internal sealed class RenameHandler(Counter counter) : IRequestHandler<Rename, string>
{
    public Task<string> Handle(Rename request, CancellationToken cancellationToken)
    {
        counter.Handled++;
        return Task.FromResult(request.Name);
    }
}
