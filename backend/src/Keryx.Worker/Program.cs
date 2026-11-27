using Keryx.Infrastructure.Hosting;
using Keryx.Worker;

// The Keryx worker: hosted services on a schedule. It serves no HTTP endpoint and runs nothing
// until the database is ready (L2-095). Settings: tools/deploy/configuration.md.
return await HostRunner.RunAsync(() => WorkerHost.Build(args), Console.Error).ConfigureAwait(false);
