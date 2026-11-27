# 0010. The collection pipeline arrives across three journeys, and runs in the Application layer

## Status

Accepted, 27 November 2026 (journey 4, worker collects on schedule).

## Context

A collection run has six stages (L2-007). Journey 4 makes runs happen on the schedule and
fetches every source safely; journey 5 stores records idempotently (`ingest-idempotently`,
`store-records`) and journey 6 publishes them (`score-and-publish`). The `deduplicate`, `store`
and `publish` stages need the `Article`, `Event`, `Organization`, `Provenance` and
`StagedRecord` tables, which do not exist before journey 5.

The run-collection-pipeline design puts `ICollectionPipeline` in `Keryx.Application` and its
implementation in `Keryx.Worker`. The backend tests run a worker's handler directly in the API
test host, which does not contain the worker's services.

Only RSS and website sources are read over plain HTTP. Instagram and Eventbrite are read
through platform APIs whose credentials are not set up, and the seeded news query needs a news
search client; neither client exists yet.

## Decision

- Journey 4 builds the `fetch`, `parse` and `classify` stages in full. `classify` uses
  `PublicationPolicy` (L2-013), which journey 6 then stores on each article. Until journey 5,
  `deduplicate`, `store` and `publish` finish `done` with zero counts, and the run's `added`,
  `updated`, `unchanged`, `duplicates` and `rejected` are 0. L2-007 AC3 and AC5 (a failed
  `store` and an unchanged API) are proved in journey 5 with the stages they describe.
- `CollectionPipeline` is a class in `Keryx.Application`, with no interface. It reaches sources
  and the database only through contracts (`ISourceFetcher`, `IFeedParser`, `IAppDbContext`),
  so the API test host runs it as the worker does.
- The fetcher already makes up to 3 attempts per source, each with the per-source time-out and
  increasing back-off (journey 3, `HttpSourceFetcher`), so the `fetch` stage does not retry
  again. A source whose fetch throws is recorded as failed with reason `refused`.
- A run fetches the `rss` and `website` sources. The other kinds are not fetched and not counted
  in `sources_total`, and their health stays as it was, until their clients are built.

## Consequences

- A run in journey 4 reports how many items are relevant, held and off-topic, but stores none of
  them; the Sources list shows each source's health, failure reason and held count.
- Journey 5 replaces the three placeholder stages and adds the store-failure tests; the
  stage words and count names stay as they are.
