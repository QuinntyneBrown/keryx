# 0008. The Sources screen arrives in two journeys, and the table takes its cells as an input

## Status

Accepted, 27 November 2026 (journey 3, staff adds a source).

## Context

Journey 3 lets staff add a source. Its dialog opens from the Sources screen
(`docs/detailed-designs/sources/add-source/README.md`), but the screen's list belongs to journey 17
(`list-sources-with-health`): kind chips, the 30-day yield, the last fetch, the row menu,
paging and the loading skeleton. A screen that showed only "No sources yet." would be wrong as
soon as staff added one.

The `table` component draws its cells itself, so its encapsulated stylesheet reaches them. Its
first version found the consumer's cell templates with a signal content query
(`contentChildren`). Diffing `--stats-json` inputs against the base showed that query adding
6.6 kB to the initial bundle, and the first `@for` (the table's rows) about 5 kB, both through
Angular core's shared runtime that the chunk optimiser keeps in the initial chunks.

## Decision

- Journey 3 builds the Sources screen's header with "Add source", its empty and error states, and
  a table of Source, Health and Region for the first page of 12, failing first, from
  `GET /admin/v1/sources`. Journey 17 adds the kind chips, the "Articles · 30d" and "Last
  fetched" columns, the row menu, the table footer with paging and the loading skeleton.
  The endpoint is built whole in journey 3, as its design gives it.
- Until runs record a failure reason (journey 4), a failing source's health reads "Failing", and
  a source that needs a look reads "Needs a look" unless it holds items for review.
- `kx-table` takes its cell templates as an input, `cells`, keyed by column, instead of a content
  query. The consumer declares each template with a reference (`#sourceCell let-row`).
- `@for` stays: every list screen needs it, and this is the one time it is paid for.

## Consequences

- The initial total goes from 227.42 kB to 235.33 kB; the budget stays at 250 kB. With the
  content query it would have been 241.94 kB.
- The shell's navigation now has Sources and Settings (ADR 0006).
- A later component that projects templates should take them as inputs too, unless a content
  query is already paid for by then.
