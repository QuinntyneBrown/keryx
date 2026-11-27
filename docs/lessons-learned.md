# Lessons learned

One entry per journey of [journeys.md](journeys.md), added before it merges. Read this file
before starting a journey and apply what fits.

Each entry records what slowed the work, the new lessons it taught and how earlier lessons
worked when applied. Newest entries go at the bottom.

## Journey 1: Scaffolding

**What slowed it**

- **A stale premise.** The work assumed the design system was on `main`; it was on another
  branch, and had to be cherry-picked before a component could be built from it. Check that
  each input a journey names is on the base branch before starting.
- **Running a mutated build.** After a mutation proof the sources were restored but not
  rebuilt, so a manual check ran the broken binary and seemed to show a leak of the query
  string into a span. Rebuild after restoring, before running anything from `bin`.
- **A metric shared by every host in the test process.** The OpenTelemetry SDK listens to a
  meter by its name, process-wide. In a test process one host's provider therefore aggregates
  every host's requests, and when any host is disposed the metric they shared is ended: the
  next host to start gets a second metric of the same name. A test that read "the"
  `http.server.request.duration` metric read the wrong one now and then: it passed every
  local run and failed on CI's first. Read every exported metric of that name, ask for a
  route no other test uses, and flush a provider once, from a host of the test's own. Spans
  cross hosts the same way: match a span by a trace id the test chose.
- **A race fixed on a guess.** The same test had failed once before, and the first fix assumed
  the span was exported before the duration was recorded. A short probe later showed the
  opposite (the measurement comes first, on the request's own thread) and the real cause
  above. Probe the order of events before fixing a race; a fix that only makes the failure
  rarer is not a fix.
- **A failure log nobody can prevent.** On a database that was never migrated, the Npgsql
  provider looks for the history table by selecting from it, so the first `migrate` logs one
  failed command. No public EF Core API avoids it; `tools/deploy/configuration.md` says to
  expect the line.
- **Long shell commands were refused.** A heredoc editing several files, or `git` joined to
  other commands, could not be checked against the worktree and did not run. Edit files with
  the editor and keep each `git` command plain and alone.

**New lessons**

- **Validate settings before the host starts.** `ValidateOnStart` alone makes the host log its
  own "Hosting failed to start" with a stack trace beside the clear line, and two failing
  options classes arrive as one `AggregateException`. `HostRunner` validates first and handles
  both shapes.
- **Write the message yourself.** A data-annotations message is wrapped in the framework's own
  sentence; `Validate(delegate, message)` gives exactly "Missing required setting: `<key>`".
- **Proving "not yet" with a fake clock.** With `FakeTimeProvider` and a `PeriodicTimer`, show
  that nothing happened by stopping the host, which finishes any check wrongly started, and
  then counting; never by waiting.
- **One in-memory store per fixture needs one root.** Hosts that must see the same data (the
  API, the CLI's entry, a test's reader) share an `InMemoryDatabaseRoot`; a store name alone
  is not shared across service providers. Replacing the provider means removing both
  `DbContextOptions<T>` and `IDbContextOptionsConfiguration<T>`, or Npgsql stays configured
  beside InMemory.
- **A span allow-list beats a block-list.** The instrumentation adds tags by version;
  `TelemetryRedactionProcessor` keeps four and drops the rest, so a new tag is private until
  someone adds it. Filter `Microsoft.AspNetCore` logs to `Warning`: their information lines
  print full URLs.
- **Prove a lazy chunk by breaking it.** The spec that the shell loads outside the initial
  bundle passed at once; importing the shell statically made it fail, which is the proof. The
  build's budget was proven the same way, by lowering it below the real total.
- **An empty slot leaves its wrapper.** A component that wraps projected content hides the
  wrapper with `:empty`, or the gap and the padding stay.
- **A health call that retries hides the answer.** `curl --retry` treats `503` as a reason to
  retry; call `/health/ready` once and read the status.
- **Run CI's steps locally first, and expect CI to differ.** Each job's steps were run as CI
  runs them before the workflow was pushed: Release for the backend, and the built app for the
  frontend (`KX_SERVE_BUILT=1`). The workflow itself was right first time; the runner's
  different timing still found the shared-metric failure above. A test that depends on what
  other test classes do at the same moment passes locally by luck.

**How earlier lessons worked**

This is the first entry; the rules in `AGENTS.md` under "Development" stood in for earlier
lessons and held. Judging every command by its exit status caught a format failure a pipe
would have hidden. "Honest red" caught tests that passed at once and proved nothing until the
rule was broken. Reading the initial total after each new Angular API kept `main` inside its
budget (223.86 kB against 250 kB). Keeping background services out of the shared test host,
and running the worker's host on its own, made every schedule test deterministic.

## Journey 2: Staff signs in

- The first endpoint-protection test returned 200 before the fallback policy and 401 after it.
  Check endpoint selection before authorization so an unknown route keeps its documented 404.
  Explicit anonymous metadata on test-only error and telemetry probes keeps their existing
  behavior observable with the production fallback enabled.
- Checking only a successful controller action misses refusals and missing routes. Register
  the admin cache header before the error and authentication middleware, then assert the header
  on each response through the API.
