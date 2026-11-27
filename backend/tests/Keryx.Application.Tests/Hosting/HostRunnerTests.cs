// Acceptance Test
// Traces to: L2-096
// Description: A process stops with exit code 2 and one line per missing setting, and any other failure is not mistaken for one

using Keryx.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Application.Tests.Hosting;

public sealed class HostRunnerTests
{
    private sealed class FirstOptions;

    private sealed class SecondOptions;

    /// <summary>A hosted service that ends the process as soon as it has started.</summary>
    private sealed class StopsAtOnce(IHostApplicationLifetime lifetime) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        }
    }

    private sealed class FailsToStart : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Not a setting.");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static HostApplicationBuilder Builder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        return builder;
    }

    [Fact]
    public async Task Given_every_setting_is_there_When_the_process_runs_and_stops_Then_it_exits_0_and_writes_nothing()
    {
        using var error = new StringWriter();

        var exitCode = await HostRunner.RunAsync(
            () =>
            {
                var builder = Builder();
                builder.Services.AddOptions<FirstOptions>().Validate(_ => true, "Missing required setting: First__Key").ValidateOnStart();
                builder.Services.AddHostedService<StopsAtOnce>();
                return builder.Build();
            },
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Given_one_setting_is_missing_When_the_process_starts_Then_it_exits_2_and_names_it()
    {
        using var error = new StringWriter();

        var exitCode = await HostRunner.RunAsync(
            () =>
            {
                var builder = Builder();
                builder.Services.AddOptions<FirstOptions>().Validate(_ => false, "Missing required setting: First__Key").ValidateOnStart();
                return builder.Build();
            },
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal($"Missing required setting: First__Key{Environment.NewLine}", error.ToString());
    }

    // Each options class validates on its own: every missing setting is named, not only the first.
    [Fact]
    public async Task Given_settings_of_two_kinds_are_missing_When_the_process_starts_Then_it_exits_2_and_names_each()
    {
        using var error = new StringWriter();

        var exitCode = await HostRunner.RunAsync(
            () =>
            {
                var builder = Builder();
                builder.Services.AddOptions<FirstOptions>()
                    .Validate(_ => false, "Missing required setting: First__Key")
                    .Validate(_ => false, "Missing required setting: First__Other")
                    .ValidateOnStart();
                builder.Services.AddOptions<SecondOptions>().Validate(_ => false, "Missing required setting: Second__Key").ValidateOnStart();
                return builder.Build();
            },
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(
            ["Missing required setting: First__Key", "Missing required setting: First__Other", "Missing required setting: Second__Key"],
            error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_a_failure_that_is_not_a_missing_setting_When_the_process_starts_Then_it_is_not_reported_as_one()
    {
        using var error = new StringWriter();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => HostRunner.RunAsync(
            () =>
            {
                var builder = Builder();
                builder.Services.AddHostedService<FailsToStart>();
                return builder.Build();
            },
            error,
            TestContext.Current.CancellationToken));

        Assert.Equal("Not a setting.", failure.Message);
        Assert.Equal(string.Empty, error.ToString());
    }
}
