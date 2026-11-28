# User journeys

The Settings percentage includes completed slices on `journey/18-change-settings` through `4b9f506`. The observability percentage follows the [operations estimate](estimates/operations.md).

| # | Name | Description | Effort (days) | Complete % |
|---|---|---|---|---|
| 1 | Scaffolding | Workspace, solution, database, migrations CLI, health checks, CI pipeline green | 5 | 100 |
| 2 | Staff signs in | Staff sign in through identity provider; non-staff refused | 4 | 100 |
| 3 | Staff adds a source | Staff add and test-fetch a feed, page or organizer | 4 | 100 |
| 4 | Worker collects on schedule | Scheduled run fetches every source safely through six pipeline stages | 8 | 100 |
| 5 | Worker ingests idempotently | Repeat runs change nothing; duplicates merge; provenance kept | 4 | 100 |
| 6 | Worker scores and publishes | Items scored for relevance, then published, held or hidden | 3 | 100 |
| 7 | Staff issues an API key | Staff create a scoped key shown once, and revoke it | 3 | 100 |
| 8 | Product reads articles | Consuming product lists and reads published articles with its key | 6 | 100 |
| 9 | Product reads events and organizations | Consuming product lists events, organizations and sources | 4 | 100 |
| 10 | Product reads reference data and status | Consuming product reads categories, regions and collection freshness | 2 | 100 |
| 11 | Staff reviews articles | Staff search, filter and open articles with provenance | 4 | 100 |
| 12 | Staff publishes or hides an article | Staff overrule the score, with Undo | 2 | 100 |
| 13 | Staff checks the Overview | Staff see last run, new articles, issues and next run | 4 | 100 |
| 14 | Staff reviews run history | Staff inspect each run's stages, counts and issues | 4 | 100 |
| 15 | Staff starts a collection now | Staff start a run on demand and follow it live | 2 | 100 |
| 16 | Staff retries failed sources | Staff refetch only a run's failed sources | 2 | 100 |
| 17 | Staff maintains sources | Staff see source health, edit and remove sources | 4 | 100 |
| 18 | Staff changes settings | Staff change schedule, scoring, geography, retention and alerts | 4 | 15 |
| 19 | Staff receives alerts | Staff e-mailed on failing sources, failed runs and daily digest | 3 | 0 |
| 20 | Staff mutes an issue | Staff silence a known issue, with Undo | 1 | 0 |
| 21 | Staff monitors API usage | Staff see traffic, latency, errors and per-product usage | 3 | 0 |
| 22 | Staff exports articles | Staff download filtered articles as CSV | 2 | 0 |
| 23 | Worker applies retention | Records past the retention period are deleted | 2 | 0 |
| 24 | Staff actions are audited | Every state-changing staff action is recorded | 2 | 0 |
| 25 | Operator observes the system | Traces, metrics and logs exported without private data | 2 | 40 |
