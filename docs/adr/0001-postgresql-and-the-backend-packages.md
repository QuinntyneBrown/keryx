# 0001. PostgreSQL and the backend packages

## Status

Accepted, 27 November 2026 (journey 1, scaffolding).

## Context

The detailed designs name "a relational database (engine `<TO SUPPLY>`) via EF Core". Journey 1
creates the first migration, so the engine had to be chosen before any code. Keryx stores text
that is searched and filtered (articles, events, organizations), lists of short strings
(topics, exclusions, focus areas, tags), and every time in UTC with its zone. Its first
consumer, Christian Fitness, already runs PostgreSQL, and the same people operate both.

`AGENTS.md` fixes the rest of the stack: clean architecture, MediatR handlers, validators,
EF Core, OpenTelemetry, and tests on EF Core InMemory with `FakeTimeProvider`.

## Decision

The database is **PostgreSQL**, through `Npgsql.EntityFrameworkCore.PostgreSQL`. The connection
string is the setting `ConnectionStrings__Keryx` ([configuration](../../tools/deploy/configuration.md)).

- `DateTimeOffset` values are stored as `timestamp with time zone` in UTC; an IANA zone that
  matters is a column beside the time.
- Lists of short strings are PostgreSQL arrays (`text[]`), which EF Core maps from
  `List<string>`.
- Migrations live in `Keryx.Infrastructure` and are applied only by `Keryx.Cli migrate`.

Package versions are pinned once in `backend/Directory.Packages.props` (central package
management). The choices that are not obvious:

| Package | Version | Why |
|---|---|---|
| MediatR | 12.5.0 | The last release under Apache 2.0; 13 and later need a licence key. |
| FluentValidation | 12.x | Validators run in one MediatR pipeline behaviour. |
| xunit.v3 | 3.x | Runs on the Microsoft testing platform; `TestContext.Current.CancellationToken`. |
| Microsoft.Extensions.TimeProvider.Testing | 10.x | `FakeTimeProvider` for every time-driven rule. |
| OpenTelemetry.* | 1.x | SDK, OTLP exporter, ASP.NET Core and `HttpClient` instrumentation. |

The SDK is pinned in `backend/global.json` and every project builds with nullable reference
types, the recommended analysers and warnings as errors (`backend/Directory.Build.props`).

## Consequences

- Christian Fitness and Keryx share one database engine, one backup and upgrade routine and
  one set of operating skills.
- The tests never touch PostgreSQL: they swap the provider for EF Core InMemory. A rule that
  only a relational engine can prove (a unique index, an execution plan) needs its fault
  injected through `IAppDbContext` or a check outside CI.
- Moving to another engine means new migrations and replacing the array columns; it is a new
  ADR.
- Upgrading MediatR past 12.5.0 is a licensing decision and needs its own ADR.
