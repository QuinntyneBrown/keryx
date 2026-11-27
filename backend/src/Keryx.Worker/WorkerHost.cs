using Keryx.Application;
using Microsoft.Extensions.Hosting;

namespace Keryx.Worker;

/// <summary>The composition of the worker process.</summary>
public static class WorkerHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddApplication();
        return builder.Build();
    }
}
