using Keryx.Cli;

// The Keryx CLI: the operator's commands. A release runs `migrate`, then `seed`, then starts
// the API and the worker. Settings: tools/deploy/configuration.md.
return await KeryxCli.RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
