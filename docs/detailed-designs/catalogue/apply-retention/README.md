# Apply retention

## Overview

Keryx keeps records only as long as staff decide. Staff set a retention period in Settings
("Keep articles for", 1 to 120 months, `L2-070`). Once a day a background worker deletes every
record older than that period, together with everything that depends only on it. This feature
is that worker and the rule it applies.

- **retention period** — number of months, set in Settings, after which a record is deleted
- **cutoff** — instant before which a record counts as expired: the time read from `TimeProvider` minus the retention period
- **retention run** — one daily execution of the retention worker, recorded like every other worker run with worker `retention`
- **dependent record** — record that exists only because of another one, such as a provenance entry of an article or a stage result of a run
- **batch** — one delete of at most 1,000 rows, committed on its own

An article expires when its collected time is earlier than the cutoff. It is deleted with its
provenance entries, and its public id is never given to another record. An event linked only to
deleted articles is deleted with them. An organization is kept while any article, event or
source refers to it, and is deleted once none does. Run records older than the cutoff are
deleted with their stage results and issues. Audit entries older than the cutoff are deleted
too (`L2-085`, designed in the operations subsystem).

The worker reads the retention period at the start of every run. A shortened period therefore
takes effect at the next run, with no backfill job. The worker is one of three workers
(`collection`, `retention`, `alert_digest`); a failure in one does not stop the others
(`L2-019`).

## Description

The slice runs from a hosted service in the worker through one MediatR command to the database.

**Worker (`Keryx.Worker`)**

- **`RetentionWorker`** — `BackgroundService` that waits for the next daily slot by reading
  `TimeProvider` and sends one `ApplyRetentionCommand` with trigger `schedule`. It checks
  nothing itself. The time of day of the slot is `<TO SUPPLY>`. A test proves the schedule by
  advancing `FakeTimeProvider`, never by sleeping. When the command throws, the worker sends
  `RecordRunFailureCommand` (run recording subsystem, `L2-019`) and waits for the next slot.

**Application (`Keryx.Application`)**

- **`ApplyRetentionCommand`** — request carrying `RunTrigger Trigger`.
- **`ApplyRetentionCommandHandler`** — opens a `Run` with worker `retention`, reads
  `Settings.RetentionMonths`, computes the cutoff from `TimeProvider`, and deletes expired
  records through `IAppDbContext` in batches of at most 1,000 rows, in this order:
  1. articles whose `CollectedAt` is earlier than the cutoff, with their provenance entries;
  2. events whose linked articles are all deleted;
  3. organizations that no article, event or source refers to;
  4. runs whose `StartedAt` is earlier than the cutoff, with their stage results and issues;
  5. audit entries whose `OccurredAt` is earlier than the cutoff.
  It finishes the run with the number deleted as a number. Whether an event with no linked
  article expires by its own collected time or by its start time is `<TO SUPPLY>`.
- **`RetentionPolicy`** — pure domain service holding `Cutoff(now, months)` and the dependency
  rules above, with a direct unit test per case.
- **`RetentionResult`** — counts per record type and their total `Deleted`.

**Domain (`Keryx.Domain`)**

- **`Settings`** — holds `RetentionMonths` (1 to 120).
- **`Article`**, **`Event`**, **`Organization`**, **`Provenance`**, **`Run`**, **`StageResult`**,
  **`RunIssue`**, **`AuditEntry`** — the entities the worker deletes.
- **`PublicId`** — prefixed public id (`art_`, `run_`, `src_`, `org_`). Generation, designed with
  stable identity (`L2-009`), never derives an id from a value that a delete frees, so a deleted
  record's id is never reused.
- **`Run`** — recorded with worker `retention`, trigger, `started_at`, `finished_at`, outcome and
  the number deleted. The name of the deleted-count field is `<TO SUPPLY>`: `L2-019` lists the
  collection counts and no `deleted` field.

**Infrastructure (`Keryx.Infrastructure`)**

- **`KeryxDbContext`** — maps the cascade from article to provenance and from run to stage
  result and issue. Each batch uses a set-based delete (`ExecuteDeleteAsync`) bounded to 1,000
  rows and commits on its own, so a failure keeps the batches already committed. Because the
  EF Core InMemory provider does not support set-based deletes, tests inject a fault through
  `IAppDbContext`; the test approach for the batch delete is `<TO SUPPLY>`.

**Admin app and API**

- No screen or endpoint is added. The retention period is edited on Settings (`L2-070`), and a
  retention run is readable through `GET /admin/v1/runs/{id}` (`L2-019`).

## Requirements

The feature realizes the following level-2 (L2) requirements. Each L2 requirement refines a
level-1 (L1) requirement, cited by identifier. Their acceptance criteria are in
[L2.md](../../../specs/L2.md).

| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-079` | `L1-019` | Retention — Records older than the retention period staff set are deleted, together with everything that depends only on them. |

## Diagrams

### System context

Retention involves no external system. Staff set the period through Keryx, and Keryx removes
what has expired.

![C4 system context for applying retention](diagrams/c4-context.png)

### Containers

The Keryx Worker deletes expired records in the Keryx database. The Keryx API serves the
retention period to Settings and the retention run to staff.

![C4 container view for applying retention](diagrams/c4-container.png)

### Components

`RetentionWorker` sends `ApplyRetentionCommand`; the handler applies `RetentionPolicy` and
deletes in batches through `IAppDbContext`, then records the run.

![C4 component view for applying retention](diagrams/c4-component.png)

### Class structure

`ApplyRetentionCommandHandler` reads `Settings`, uses `RetentionPolicy` for the cutoff, and
records a `Run` with a `RetentionResult`.

![Class diagram for applying retention](diagrams/class-structure.png)

### Behaviour — apply retention

At the daily slot the worker sends one command. The handler deletes expired articles, dependent
events, orphaned organizations, old runs and old audit entries, at most 1,000 rows per batch,
and records the number deleted.

![Sequence diagram for applying retention](diagrams/sequence-apply-retention.png)

### Behaviour — a retention run fails

A failure part-way keeps the committed batches, records the run as `failed` with a generic
reason, and leaves the next daily run and the other workers unaffected.

![Sequence diagram for a failed retention run](diagrams/sequence-retention-failure.png)
