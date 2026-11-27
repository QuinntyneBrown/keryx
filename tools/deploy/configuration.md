# Configuration

Every setting a deployed Keryx system needs, and what a release runs. A setting joins this page
in the same change that introduces it or changes it (`L2-096`), and its name is final from then
on.

Settings are environment variables. A double underscore separates the parts of a name
(`ConnectionStrings__Keryx`). No setting is read from a file in the repository, and no secret is
committed.

## Settings

| Setting | Meaning | Default | Secret | Read by |
|---------|---------|---------|--------|---------|
| `ConnectionStrings__Keryx` | The connection string of the Keryx database (PostgreSQL), for example `Host=…;Database=keryx;Username=…;Password=…`. | None: required. | Yes | API, worker, CLI |

## A required setting that is missing

A process checks its required settings as it starts. When one is missing, empty or blank, the
process does not start: it writes one line per setting to standard error and exits with code
`2`. The line names the setting and never a value:

```text
Missing required setting: ConnectionStrings__Keryx
```

## What a release runs

The three processes are built from `backend/`. A release runs them in this order:

1. `Keryx.Cli migrate` applies the pending database migrations. It is the only thing that
   changes the schema: the API and the worker never migrate. Run again, it changes nothing.
2. `Keryx.Cli seed` adds the seed data that is missing: the regions, the categories, the default
   settings and the seeded news query sources. Run again, it changes nothing, and a row staff
   have changed since is left as it is.
3. Start `Keryx.Api` and `Keryx.Worker`.

| Exit code | Meaning |
|-----------|---------|
| `0` | The command did its work, or found nothing to do. |
| `1` | The command line names no command; the usage is printed. |
| `2` | A required setting is missing. |

Each command prints what it did as numbers, on one line:

```text
Migrations applied: 1
Seed data added: regions 14, categories 6, settings 1, sources 1
```

On a database that has never been migrated, the first `migrate` also logs a failed
`SELECT … FROM "__EFMigrationsHistory"`. That is how the database driver learns the history
table is not there yet. It is expected once; the exit code is the result.

## Health

Neither endpoint needs a credential. Each answers with one word as plain text and reveals no
version, host or error.

| Endpoint | Answers | Use it for |
|----------|---------|------------|
| `GET /health/live` | `200` `Healthy` while the process runs. It never touches the database. | Restarting a process that hangs. |
| `GET /health/ready` | `200` `Healthy` only when the database is reachable and its schema is exactly at the version the build expects; otherwise `503` `Unhealthy`. | Sending traffic to an instance. |

When the API is not ready it says why once, in its log, as it starts: the schema is behind the
build (run `Keryx.Cli migrate`), the schema was changed by a newer build (deploy that build), or
the database cannot be reached.

The worker serves no endpoint. Until the database is ready it runs no worker, logs the same
reason once, and asks again every 30 seconds; it logs one line when the database is ready.
