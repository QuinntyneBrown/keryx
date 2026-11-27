using Keryx.Api;
using Keryx.Infrastructure.Hosting;

// The Keryx API. It never migrates on startup: `Keryx.Cli migrate` does (L2-095).
// Settings: tools/deploy/configuration.md.
return await HostRunner.RunAsync(() => ApiHost.Build(args), Console.Error).ConfigureAwait(false);
