# Overview

## Overview

Keryx collects Christian and fitness news from a catalogue of sources on a schedule, every
6 hours by default, Toronto time. Overview is the first screen of Keryx Admin. It answers four
questions at a glance: did the last run work, what did it bring in, what needs attention, and
when is the next run.

**run** — one execution of the collection worker over every enabled source, recorded with its
outcome and counts

**outcome** — final state of a run: `succeeded`, `completed_with_issues`, `failed`, or `running`
while it is in progress

**status card** — top card of Overview that summarizes the last run, or the current one, with
three stats and the run chart

**run chart** — bar chart of the new-article count of each of the last 14 collection runs,
oldest first

**issue** — problem found during a run, with a severity (error or warning), a kind, a subject
and a streak of consecutive runs; an open issue is one that is neither resolved nor muted

**section** — one independently loaded part of Overview: status, run chart, latest articles or
needs attention

The status card reads "Last run" with an outcome headline such as "Completed with 2 failing
sources", a meta line ("Today, 6:00 AM · took 4m 12s · run_2418") and the stats New articles,
Sources OK and Next run. While a run is in progress it reads "Current run", "Collecting news
now…", shows a progress bar and replaces "Run now" with a disabled "Collecting…", and its counts
update without a reload. Below it, "Latest articles" lists the 7 newest articles of any status
and "Needs attention" lists up to 5 open issues, errors first. Each run, issue and list links to
the screen where it can be acted on.

Each section loads and fails on its own, so one failed request leaves the rest of the page
usable. Overview has designed loading, empty and error states. The "Run now" action and its
notifications belong to the Run collection now design (`L2-047`); Overview only hosts the
button.

## Description

The slice runs from the routed Overview page to four admin read endpoints.

**Frontend — `frontend/projects/admin`**

- **`OverviewPage`** — routed page loaded with `loadComponent`
  (`pages/overview/overview-page.ts`, `.html`, `.scss`). It provides `OverviewStore` in its own
  `providers`, so the store and its timers are released when the page is left (`L2-089`). It
  composes the page header ("Overview", "Christian fitness news collected across Toronto & the
  GTA."), the status card, the run chart inside a `@defer` block (`L2-088`) and the two lists in
  a `layout-rail` that stacks below 1024 px and sits side by side from 1024 px. It renders the
  empty state when the store reports `isEmpty`, and the page error state of `L2-073` when
  `allFailed`.
- **`OverviewStore`** — screen store held in signals. It holds four `Section` values (`status`,
  `chart`, `latest`, `issues`), each with `state` (`idle`, `loading`, `ready`, `error`), `data`,
  `errorCode` and `reference`. `load()` sends the four requests in parallel. A 300 ms timer sets
  `showSkeleton` only when no answer has arrived by then; the main region then carries
  `aria-busy="true"`. `retry(section)` repeats one request. `isEmpty` is true when the status
  reports no source and no run. While `current_run` is set, it polls `RUNS_API.getRun(id)` no
  more often than every 3 seconds, keeps only the latest response and stops when the outcome is
  no longer `running` or the page is destroyed. When the run ends it reloads `status` and
  `chart` and passes the final run to `CollectionWatcher`.
- **`CollectionWatcher`** — app-wide watcher owned by the Page states, dialogs and notifications
  design. It raises the outcome notification of `L2-076` once per run.
- **`ScheduleStore`** — the shell's schedule state (Admin shell design). The Next run stat
  ("12:00 PM", "in 2h 14m") is read from it; Overview issues no request of its own for it.
- **View mapping** — the store maps DTOs to view models: outcome headline ("Completed with
  2 failing sources", "Completed — all sources collected", "Run failed — database unreachable"
  chosen by `failure_code`), meta line, Sources OK ("62 /64"), bars and issue cards. Text from a
  source (titles, organization and subject names) is bound as text, never as HTML.

**Frontend — `frontend/projects/components`** (presentation only, named by mock block)

- **`PageHeader`** (`page-header`) — title, description and the actions slot holding "Run now".
- **`StatusCard`** (`status-card`, elements `status-card__main`, `__summary`, `__icon`,
  `__eyebrow`, `__headline`, `__meta`, `__stats`, `__history`; icon modifiers `--success`,
  `--warning`, `--danger`, `--info`) — the outcome as icon and words; the outcome is in the
  headline text, not in colour alone. It shows `progress` with an accessible value while
  running. Its title, meta line and action wrap; the run id is never broken.
- **`Stat`** (`stat`, in a `stats` row) — label, value, unit ("/64") and sub-line; the three
  stats stay in one row at every viewport class.
- **`RunChart`** (`run-chart`, element `run-chart__bar` with `data-status` `success`,
  `warning`, `failed`, `running`) — one bar per run, oldest first, each focusable and labelled
  "Today 6:00 AM: 47 new", "…: failed" or "…: running"; a failed bar is marked by more than
  colour. It emits `openRun(runId)` on pointer or keyboard activation. All bars fit inside the
  card at every viewport class.
- **`SectionHead`** (`section-head`, element `section-head__link`) — section title with "View
  all 18,432", "All runs" or the open count ("3 open").
- **`List`** (`list`, elements `list__item`, `__body`, `__title`, `__meta`, `__aside`) — latest
  articles with title, "organization · region · category", relative age and a `Badge` "Review"
  on any that needs review. Long titles wrap inside their item.
- **`IssueCard`** (`issue-card`, elements `issue-card__body`, `__title`, `__source`, `__meta`) —
  one open issue as a link, with severity as an icon and a visually hidden word ("Error: ",
  "Warning: "), its title, its subject and its time or streak ("4 runs in a row · since Mon,
  12:02 PM").
- **`Empty`** (`empty`, modifiers `--card`, `--compact`, `--error`, `--success`) — the empty
  state, the section error ("Latest articles didn't load", "Try again"), the page error, and
  "Nothing needs attention" / "Every source responded in the last run.".
- **`Skeleton`** (`skeleton`) — placeholders in the shape of the final layout; no animation when
  reduced motion is preferred.
- **`Badge`**, **`Btn`**, **`Card`**, **`Progress`** — shared blocks reused as in the mocks.

**Frontend — `frontend/projects/domain`**

- **`formatRelativeAge(at, now)`** — "2h ago", minutes under an hour, hours under a day,
  otherwise days.
- **`formatDuration(ms)`** — "4m 12s".
- **`formatTorontoTime(at, now)`** — "Today, 6:00 AM", "Mon, 12:02 PM" in Toronto time.
- **`formatCountdown(now, at)`** — "in 2h 14m" (Admin shell design).

**Frontend — `frontend/projects/api`**

- **`OverviewApi` / `OVERVIEW_API`** — `getStatus(): Observable<OverviewStatusDto>`.
- **`RunsApi` / `RUNS_API`** — `listRuns(query)` and `getRun(id)`, owned by the View run history
  design, and `listOpenIssues(pageSize)`, beside the `muteIssue(id)` and `unmuteIssue(id)` calls
  of the Mute an issue design.
- **`ArticlesApi` / `ARTICLES_API`** — `list(query)`, owned by the Review articles design.

**Endpoints** (all under `StaffPolicy`, `Cache-Control: no-store`; `401` `session_expired` and
`403` `not_staff` per the Staff sign-in and session design)

| Endpoint | Request | Response | Errors |
|---|---|---|---|
| `GET /admin/v1/overview/status` | — | `last_run` and `current_run` (each `null` or `id`, `started_at`, `finished_at`, `duration_ms`, `outcome`, `failure_code`, `added`, `sources_ok`, `sources_checked`, `sources_total`), `has_sources`, `has_runs`, `next_run_at` (UTC) and `time_zone` | ProblemDetails with trace id |
| `GET /admin/v1/runs` | `worker=collection`, `page=1`, `page_size=14` | page envelope of `RunSummaryDto` (`id`, `started_at`, `outcome`, `added`, …), newest first; the store reverses it to oldest first | View run history design |
| `GET /admin/v1/runs/{id}` | — | `RunDto` with `sources_checked`, `sources_total`, `added` while running | View run history design |
| `GET /admin/v1/articles` | `page=1`, `page_size=7` | `items`, `total`, `page`, `page_size`; `total` gives "View all 18,432" | Review articles design |
| `GET /admin/v1/issues` (path `<TO SUPPLY>`) | `state=open`, `page_size=5` | `items` of `id`, `run_id`, `severity`, `kind`, `subject_name`, `consecutive_runs`, `since`; `total` gives "3 open" | ProblemDetails with trace id |

**Backend**

- **`AdminOverviewController`** — `GetStatus` sends one `GetOverviewStatusQuery`.
- **`GetOverviewStatusQueryHandler`** (built as `GetOverviewStatus`) — reads the latest finished
  collection `Run`, the running one if any, and whether any `Source` and any `Run` exist, through
  `IAppDbContext`. It serves counts and durations as numbers and the failure as a `failure_code`:
  `<stage>_failed` when a stage failed (`store_failed` reads "database unreachable"),
  `interrupted` when a restart cut the run short, otherwise `unexpected`. It also serves the next
  run's time from `GetNextRun`, so Overview's Next run stat needs no request of its own; the
  shell's `ScheduleStore` is not built yet, and Overview reads the time from its own status until
  it is. A seeded news query counts as a source.
- **`AdminIssuesController`** / **`GetOpenIssuesQuery`** / **`GetOpenIssuesQueryHandler`** —
  read `RunIssue` rows that are open and not muted, ordered errors before warnings, then by time
  `<TO SUPPLY>` (the order within a severity is not stated), bounded by `page_size`.
- **`AdminRunsController`**, **`AdminArticlesController`** — reused as defined by the View run
  history and Review articles designs.

**Navigation targets** — a bar opens `/runs?run=<id>`; an issue card opens the run it belongs to
in the same tab; "All runs" opens `/runs`; with more than 5 open issues a link opens
`/runs?problems=true`; "View all 18,432" opens `/articles`. "Add a source" opens Sources and
"Review the schedule" opens Settings; the exact targets are `<TO SUPPLY>`. The issue title text
for each `kind` ("Feed returned 404", "Timed out after 30s") is `<TO SUPPLY>` pending the Record
runs and issues design.

## Requirements

The feature realizes the following level-2 (L2) requirements. Each L2 requirement refines a
level-1 (L1) requirement, cited by identifier. Their acceptance criteria are in
[L2.md](../../../specs/L2.md).

| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-043` | `L1-011` | Overview status card — The top of Overview tells staff at a glance how the last run went, or how the current one is going, and when the next one is. |
| `L2-044` | `L1-011` | Overview run chart — Overview shows the new-article count of each of the last 14 collection runs so staff can spot a drop or a failure. |
| `L2-045` | `L1-011` | Overview latest articles and needs attention — Overview lists the newest articles and the open issues that need staff attention, each linking to where it can be acted on. |
| `L2-046` | `L1-011` | Overview loading, empty and error — Overview has designed loading, empty and error states. |

## Diagrams

### System context

Staff read Overview in Keryx, which collects from the sources on a schedule.

![C4 system context for Overview](diagrams/c4-context.png)

### Containers

Keryx Admin reads status, runs, latest articles and open issues from the Keryx API. The Keryx
Worker records the runs, their progress and their issues in the Keryx database.

![C4 container view for Overview](diagrams/c4-container.png)

### Components

`OverviewPage` composes library components and reads `OverviewStore`, which calls four contracts.
Each contract reaches one admin controller that sends one query to its handler.

![C4 component view for Overview](diagrams/c4-component.png)

### Class structure

`OverviewStore` owns four `Section` values, depends on four API contracts, and reads
`ScheduleStore` and `CollectionWatcher`. On the server, `GetOverviewStatusQueryHandler` reads
`Run` and `RunIssue`.

![Class diagram for Overview](diagrams/class-structure.png)

### Behaviour — load Overview

The store sends four requests in parallel and shows skeletons only after 300 ms. The page then
shows the empty state, the page error, a section error, or the full Overview.

![Sequence diagram for loading Overview](diagrams/sequence-load-overview.png)

### Behaviour — follow a running collection

While a run is in progress, the store polls it at most every 3 seconds and keeps only the latest
progress. When it ends, the card returns to "Last run" and `CollectionWatcher` reports the
outcome once.

![Sequence diagram for following a running collection](diagrams/sequence-follow-running-run.png)

### Behaviour — open a run from Overview

A bar, an issue card, "All runs" or the problems link navigates in the same tab to the Runs
screen, which loads lazily and reads the named run.

![Sequence diagram for opening a run from Overview](diagrams/sequence-open-from-overview.png)
