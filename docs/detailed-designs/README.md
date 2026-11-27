# Keryx detailed designs

Each folder below holds the detailed design of one vertical feature slice, written against the level-2 (L2) requirements in [`docs/specs/L2.md`](../specs/L2.md). Each design page gives an overview, the building blocks of the slice, the L2 requirements it realizes, and C4, class and sequence diagrams. The PlantUML sources sit beside the rendered images in each `diagrams/` folder.

The designs describe a greenfield system: the repository holds no product code yet, so each type name is introduced by its design. The names follow the layering in [`AGENTS.md`](../../AGENTS.md): `Keryx.Domain`, `Keryx.Application`, `Keryx.Infrastructure`, `Keryx.Api`, `Keryx.Worker` and `Keryx.Cli` in the backend, and the `admin` application with the `components`, `domain` and `api` libraries in the frontend. A detail the requirements leave undecided is marked `<TO SUPPLY>`.

## Sources

The catalogue of places Keryx collects from, and the staff screen and dialogs that maintain it.

| Feature | L2 requirements |
|---------|-----------------|
| [Add a source](sources/add-source/README.md) | `L2-001`, `L2-017`, `L2-064` |
| [Test-fetch a source](sources/test-fetch-source/README.md) | `L2-002` |
| [Edit and remove a source](sources/edit-and-remove-source/README.md) | `L2-003`, `L2-065` |
| [Browse sources and their health](sources/browse-sources/README.md) | `L2-004`, `L2-062`, `L2-063`, `L2-066` |

## Collection

The collection worker: its schedule, its six-stage pipeline, safe fetching, identity, scoring and run recording.

| Feature | L2 requirements |
|---------|-----------------|
| [Schedule collection](collection/schedule-collection/README.md) | `L2-005` |
| [Start a run on demand](collection/start-run-on-demand/README.md) | `L2-006`, `L2-047` |
| [Run the collection pipeline](collection/run-collection-pipeline/README.md) | `L2-007`, `L2-008`, `L2-087` |
| [Retry failed sources](collection/retry-failed-sources/README.md) | `L2-008`, `L2-059` |
| [Fetch safely and respectfully](collection/fetch-safely/README.md) | `L2-021`, `L2-022`, `L2-023`, `L2-024` |
| [Identify and deduplicate records](collection/identify-and-deduplicate/README.md) | `L2-009`, `L2-010`, `L2-011` |
| [Score relevance and classify](collection/score-and-classify/README.md) | `L2-012`, `L2-013` |
| [Record runs and issues](collection/record-runs-and-issues/README.md) | `L2-019`, `L2-020` |

## Catalogue

The stored records (articles, events, organizations, regions) and the rules that bound and expire them.

| Feature | L2 requirements |
|---------|-----------------|
| [Store articles, events and organizations](catalogue/store-records/README.md) | `L2-015`, `L2-016`, `L2-017`, `L2-018`, `L2-080`, `L2-081` |
| [Apply retention](catalogue/apply-retention/README.md) | `L2-079` |

## Consumer API

The versioned `/v1` API other products call, its clients, keys, scopes, limits and usage.

| Feature | L2 requirements |
|---------|-----------------|
| [Serve a versioned, bounded contract](consumer-api/serve-versioned-contract/README.md) | `L2-025`, `L2-031`, `L2-032`, `L2-086` |
| [Read published articles](consumer-api/read-articles/README.md) | `L2-026`, `L2-027` |
| [Read events](consumer-api/read-events/README.md) | `L2-028` |
| [Read organizations and sources](consumer-api/read-organizations-and-sources/README.md) | `L2-029` |
| [Read categories, regions and status](consumer-api/read-reference-data-and-status/README.md) | `L2-030` |
| [Authenticate, scope and rate-limit API clients](consumer-api/authenticate-api-clients/README.md) | `L2-033`, `L2-034`, `L2-038` |
| [Create an API key](consumer-api/create-api-key/README.md) | `L2-035`, `L2-068` |
| [Revoke an API key](consumer-api/revoke-api-key/README.md) | `L2-036`, `L2-069` |
| [View API usage and consumers](consumer-api/view-api-usage/README.md) | `L2-037`, `L2-067` |

## Staff access

Staff sign-in through OpenID Connect, the staff policy and the session.

| Feature | L2 requirements |
|---------|-----------------|
| [Sign in staff and keep the session](staff-access/sign-in-staff/README.md) | `L2-039`, `L2-041` |
| [Authorize staff on every endpoint](staff-access/authorize-staff/README.md) | `L2-040` |

## Admin console

The admin application: shell, Overview, Articles, Runs, Settings and the shared feedback behaviour.

| Feature | L2 requirements |
|---------|-----------------|
| [Admin shell](admin-console/admin-shell/README.md) | `L2-042` |
| [Overview](admin-console/overview/README.md) | `L2-043`, `L2-044`, `L2-045`, `L2-046` |
| [Browse, search and filter articles](admin-console/browse-articles/README.md) | `L2-048`, `L2-049`, `L2-050`, `L2-055` |
| [View an article's detail](admin-console/view-article-detail/README.md) | `L2-051`, `L2-053` |
| [Publish, hide and undo an article decision](admin-console/decide-article/README.md) | `L2-014`, `L2-052` |
| [Export articles as CSV](admin-console/export-articles/README.md) | `L2-054` |
| [Browse runs and a run's detail](admin-console/browse-runs/README.md) | `L2-056`, `L2-057`, `L2-061` |
| [Watch a running run and report its outcome](admin-console/watch-running-run/README.md) | `L2-058`, `L2-076` |
| [Mute an issue from the admin application](admin-console/mute-issue/README.md) | `L2-060` |
| [Edit and save settings](admin-console/edit-settings/README.md) | `L2-070`, `L2-071`, `L2-072` |
| [Page states, dialogs and notifications](admin-console/show-feedback/README.md) | `L2-073`, `L2-074`, `L2-075` |

## Alerts

E-mail alerts for failing sources and failed runs, and the daily digest of held articles.

| Feature | L2 requirements |
|---------|-----------------|
| [Alert on failing sources and failed runs](alerts/send-failure-alerts/README.md) | `L2-077` |
| [Send the daily digest of held articles](alerts/send-daily-digest/README.md) | `L2-078` |

## Platform

Cross-cutting qualities: security, audit, telemetry, operations, frontend efficiency, accessibility and brand.

| Feature | L2 requirements |
|---------|-----------------|
| [Secure transport, input and secrets](platform/secure-transport-input-and-secrets/README.md) | `L2-082`, `L2-083`, `L2-084` |
| [Audit staff actions](platform/audit-staff-actions/README.md) | `L2-085` |
| [Emit telemetry without private data](platform/emit-telemetry/README.md) | `L2-094` |
| [Health, migrations, seed data and configuration](platform/operate-deployment/README.md) | `L2-095`, `L2-096` |
| [Lazy loading and memory in the admin application](platform/keep-frontend-lean/README.md) | `L2-088`, `L2-089` |
| [Responsive, accessible and themed UI](platform/responsive-accessible-ui/README.md) | `L2-090`, `L2-091`, `L2-092`, `L2-093` |
| [Brand and design-system conformance](platform/brand-and-design-system/README.md) | `L2-097`, `L2-098` |

## Requirement coverage

Each L2 requirement is realized by at least one feature design.

| L2 ID | Requirement | Refines (L1) | Designed in |
|-------|-------------|--------------|-------------|
| `L2-001` | Source record | `L1-001` Source catalogue | [Add a source](sources/add-source/README.md) |
| `L2-002` | Source test fetch | `L1-001` Source catalogue | [Test-fetch a source](sources/test-fetch-source/README.md) |
| `L2-003` | Edit and remove a source | `L1-001` Source catalogue | [Edit and remove a source](sources/edit-and-remove-source/README.md) |
| `L2-004` | Source health | `L1-001` Source catalogue | [Browse sources and their health](sources/browse-sources/README.md) |
| `L2-005` | Collection schedule | `L1-002` Scheduled and on-demand collection | [Schedule collection](collection/schedule-collection/README.md) |
| `L2-006` | Run on demand | `L1-002` Scheduled and on-demand collection | [Start a run on demand](collection/start-run-on-demand/README.md) |
| `L2-007` | Collection pipeline stages | `L1-002` Scheduled and on-demand collection | [Run the collection pipeline](collection/run-collection-pipeline/README.md) |
| `L2-008` | Source isolation and retry within a run | `L1-002` Scheduled and on-demand collection | [Run the collection pipeline](collection/run-collection-pipeline/README.md), [Retry failed sources](collection/retry-failed-sources/README.md) |
| `L2-009` | Stable identity | `L1-003` Ingestion integrity and provenance | [Identify and deduplicate records](collection/identify-and-deduplicate/README.md) |
| `L2-010` | Deduplication across sources | `L1-003` Ingestion integrity and provenance | [Identify and deduplicate records](collection/identify-and-deduplicate/README.md) |
| `L2-011` | Provenance | `L1-003` Ingestion integrity and provenance | [Identify and deduplicate records](collection/identify-and-deduplicate/README.md) |
| `L2-012` | Relevance scoring | `L1-004` Relevance and publication | [Score relevance and classify](collection/score-and-classify/README.md) |
| `L2-013` | Publication status from thresholds | `L1-004` Relevance and publication | [Score relevance and classify](collection/score-and-classify/README.md) |
| `L2-014` | Staff decisions on an article | `L1-004` Relevance and publication | [Publish, hide and undo an article decision](admin-console/decide-article/README.md) |
| `L2-015` | Article record | `L1-005` The record catalogue | [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-016` | Event record | `L1-005` The record catalogue | [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-017` | Organization record | `L1-005` The record catalogue | [Add a source](sources/add-source/README.md), [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-018` | Regions and time | `L1-005` The record catalogue | [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-019` | Run record | `L1-006` Run recording | [Record runs and issues](collection/record-runs-and-issues/README.md) |
| `L2-020` | Run issues and muting | `L1-006` Run recording | [Record runs and issues](collection/record-runs-and-issues/README.md) |
| `L2-021` | Robots, terms and identification | `L1-007` Respectful and safe fetching | [Fetch safely and respectfully](collection/fetch-safely/README.md) |
| `L2-022` | Rate limiting, back-off and time-outs | `L1-007` Respectful and safe fetching | [Fetch safely and respectfully](collection/fetch-safely/README.md) |
| `L2-023` | Fetched content is untrusted | `L1-007` Respectful and safe fetching | [Fetch safely and respectfully](collection/fetch-safely/README.md) |
| `L2-024` | No requests to private or internal addresses | `L1-007` Respectful and safe fetching | [Fetch safely and respectfully](collection/fetch-safely/README.md) |
| `L2-025` | API versioning and contract | `L1-008` The consumer API | [Serve a versioned, bounded contract](consumer-api/serve-versioned-contract/README.md) |
| `L2-026` | List articles | `L1-008` The consumer API | [Read published articles](consumer-api/read-articles/README.md) |
| `L2-027` | Read one article | `L1-008` The consumer API | [Read published articles](consumer-api/read-articles/README.md) |
| `L2-028` | Events endpoints | `L1-008` The consumer API | [Read events](consumer-api/read-events/README.md) |
| `L2-029` | Organizations and sources endpoints | `L1-008` The consumer API | [Read organizations and sources](consumer-api/read-organizations-and-sources/README.md) |
| `L2-030` | Categories, regions and status endpoints | `L1-008` The consumer API | [Read categories, regions and status](consumer-api/read-reference-data-and-status/README.md) |
| `L2-031` | Consumer API errors | `L1-008` The consumer API | [Serve a versioned, bounded contract](consumer-api/serve-versioned-contract/README.md) |
| `L2-032` | Caching for consumers | `L1-008` The consumer API | [Serve a versioned, bounded contract](consumer-api/serve-versioned-contract/README.md) |
| `L2-033` | API key authentication | `L1-009` API clients and keys | [Authenticate, scope and rate-limit API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-034` | Scopes | `L1-009` API clients and keys | [Authenticate, scope and rate-limit API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-035` | Creating a key | `L1-009` API clients and keys | [Create an API key](consumer-api/create-api-key/README.md) |
| `L2-036` | Revoking a key | `L1-009` API clients and keys | [Revoke an API key](consumer-api/revoke-api-key/README.md) |
| `L2-037` | Usage metrics per client | `L1-009` API clients and keys | [View API usage and consumers](consumer-api/view-api-usage/README.md) |
| `L2-038` | Rate limiting clients | `L1-009` API clients and keys | [Authenticate, scope and rate-limit API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-039` | Staff sign-in | `L1-010` Staff authentication and authorization | [Sign in staff and keep the session](staff-access/sign-in-staff/README.md) |
| `L2-040` | Staff authorization | `L1-010` Staff authentication and authorization | [Authorize staff on every endpoint](staff-access/authorize-staff/README.md) |
| `L2-041` | Session lifetime and sign-out | `L1-010` Staff authentication and authorization | [Sign in staff and keep the session](staff-access/sign-in-staff/README.md) |
| `L2-042` | Admin shell | `L1-011` Admin shell and Overview | [Admin shell](admin-console/admin-shell/README.md) |
| `L2-043` | Overview status card | `L1-011` Admin shell and Overview | [Overview](admin-console/overview/README.md) |
| `L2-044` | Overview run chart | `L1-011` Admin shell and Overview | [Overview](admin-console/overview/README.md) |
| `L2-045` | Overview latest articles and needs attention | `L1-011` Admin shell and Overview | [Overview](admin-console/overview/README.md) |
| `L2-046` | Overview loading, empty and error | `L1-011` Admin shell and Overview | [Overview](admin-console/overview/README.md) |
| `L2-047` | Run now from the admin app | `L1-011` Admin shell and Overview | [Start a run on demand](collection/start-run-on-demand/README.md) |
| `L2-048` | Articles list | `L1-012` Article review | [Browse, search and filter articles](admin-console/browse-articles/README.md) |
| `L2-049` | Article status tabs | `L1-012` Article review | [Browse, search and filter articles](admin-console/browse-articles/README.md) |
| `L2-050` | Article search and filters | `L1-012` Article review | [Browse, search and filter articles](admin-console/browse-articles/README.md) |
| `L2-051` | Article detail panel | `L1-012` Article review | [View an article's detail](admin-console/view-article-detail/README.md) |
| `L2-052` | Publish and hide from the admin app | `L1-012` Article review | [Publish, hide and undo an article decision](admin-console/decide-article/README.md) |
| `L2-053` | Untrusted and extreme article text | `L1-012` Article review | [View an article's detail](admin-console/view-article-detail/README.md) |
| `L2-054` | Export articles as CSV | `L1-012` Article review | [Export articles as CSV](admin-console/export-articles/README.md) |
| `L2-055` | Articles loading, empty and error | `L1-012` Article review | [Browse, search and filter articles](admin-console/browse-articles/README.md) |
| `L2-056` | Runs list | `L1-013` Run history | [Browse runs and a run's detail](admin-console/browse-runs/README.md) |
| `L2-057` | Run detail | `L1-013` Run history | [Browse runs and a run's detail](admin-console/browse-runs/README.md) |
| `L2-058` | Running run | `L1-013` Run history | [Watch a running run and report its outcome](admin-console/watch-running-run/README.md) |
| `L2-059` | Retry failed sources | `L1-013` Run history | [Retry failed sources](collection/retry-failed-sources/README.md) |
| `L2-060` | Mute an issue from the admin app | `L1-013` Run history | [Mute an issue from the admin application](admin-console/mute-issue/README.md) |
| `L2-061` | Runs loading, empty and error | `L1-013` Run history | [Browse runs and a run's detail](admin-console/browse-runs/README.md) |
| `L2-062` | Sources list | `L1-014` Source management screen | [Browse sources and their health](sources/browse-sources/README.md) |
| `L2-063` | Source row menu | `L1-014` Source management screen | [Browse sources and their health](sources/browse-sources/README.md) |
| `L2-064` | Add a source dialog | `L1-014` Source management screen | [Add a source](sources/add-source/README.md) |
| `L2-065` | Edit and remove source dialogs | `L1-014` Source management screen | [Edit and remove a source](sources/edit-and-remove-source/README.md) |
| `L2-066` | Sources loading, empty and error | `L1-014` Source management screen | [Browse sources and their health](sources/browse-sources/README.md) |
| `L2-067` | API screen | `L1-015` API management screen | [View API usage and consumers](consumer-api/view-api-usage/README.md) |
| `L2-068` | New API key dialog and one-time reveal | `L1-015` API management screen | [Create an API key](consumer-api/create-api-key/README.md) |
| `L2-069` | Revoke API key dialog | `L1-015` API management screen | [Revoke an API key](consumer-api/revoke-api-key/README.md) |
| `L2-070` | Settings form | `L1-016` Settings | [Edit and save settings](admin-console/edit-settings/README.md) |
| `L2-071` | Saving settings | `L1-016` Settings | [Edit and save settings](admin-console/edit-settings/README.md) |
| `L2-072` | Chip lists and the Add a topic dialog | `L1-016` Settings | [Edit and save settings](admin-console/edit-settings/README.md) |
| `L2-073` | Page states | `L1-017` Feedback, states and notifications | [Page states, dialogs and notifications](admin-console/show-feedback/README.md) |
| `L2-074` | Dialog behaviour | `L1-017` Feedback, states and notifications | [Page states, dialogs and notifications](admin-console/show-feedback/README.md) |
| `L2-075` | Notifications | `L1-017` Feedback, states and notifications | [Page states, dialogs and notifications](admin-console/show-feedback/README.md) |
| `L2-076` | Collection outcome notifications | `L1-017` Feedback, states and notifications | [Watch a running run and report its outcome](admin-console/watch-running-run/README.md) |
| `L2-077` | Alerts for failing sources and failed runs | `L1-018` Operational alerts | [Alert on failing sources and failed runs](alerts/send-failure-alerts/README.md) |
| `L2-078` | Daily digest of held articles | `L1-018` Operational alerts | [Send the daily digest of held articles](alerts/send-daily-digest/README.md) |
| `L2-079` | Retention | `L1-019` Privacy and source respect | [Apply retention](catalogue/apply-retention/README.md) |
| `L2-080` | Facts and a link, not a copy | `L1-019` Privacy and source respect | [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-081` | Personal data | `L1-019` Privacy and source respect | [Store articles, events and organizations](catalogue/store-records/README.md) |
| `L2-082` | Transport and browser security | `L1-020` Security | [Secure transport, input and secrets](platform/secure-transport-input-and-secrets/README.md) |
| `L2-083` | Input validation and injection | `L1-020` Security | [Secure transport, input and secrets](platform/secure-transport-input-and-secrets/README.md) |
| `L2-084` | Secrets | `L1-020` Security | [Secure transport, input and secrets](platform/secure-transport-input-and-secrets/README.md) |
| `L2-085` | Audit of staff actions | `L1-020` Security | [Audit staff actions](platform/audit-staff-actions/README.md) |
| `L2-086` | API performance | `L1-021` Performance and efficiency | [Serve a versioned, bounded contract](consumer-api/serve-versioned-contract/README.md) |
| `L2-087` | Collection performance | `L1-021` Performance and efficiency | [Run the collection pipeline](collection/run-collection-pipeline/README.md) |
| `L2-088` | Initial bundle and lazy loading | `L1-021` Performance and efficiency | [Lazy loading and memory in the admin application](platform/keep-frontend-lean/README.md) |
| `L2-089` | Memory over a long session | `L1-021` Performance and efficiency | [Lazy loading and memory in the admin application](platform/keep-frontend-lean/README.md) |
| `L2-090` | Responsive layout | `L1-022` Responsive design and accessibility | [Responsive, accessible and themed UI](platform/responsive-accessible-ui/README.md) |
| `L2-091` | Keyboard and focus | `L1-022` Responsive design and accessibility | [Responsive, accessible and themed UI](platform/responsive-accessible-ui/README.md) |
| `L2-092` | Semantics and contrast | `L1-022` Responsive design and accessibility | [Responsive, accessible and themed UI](platform/responsive-accessible-ui/README.md) |
| `L2-093` | Themes and preferences | `L1-022` Responsive design and accessibility | [Responsive, accessible and themed UI](platform/responsive-accessible-ui/README.md) |
| `L2-094` | Telemetry | `L1-023` Observability and operations | [Emit telemetry without private data](platform/emit-telemetry/README.md) |
| `L2-095` | Health, migrations and seed data | `L1-023` Observability and operations | [Health, migrations, seed data and configuration](platform/operate-deployment/README.md) |
| `L2-096` | Documented configuration | `L1-023` Observability and operations | [Health, migrations, seed data and configuration](platform/operate-deployment/README.md) |
| `L2-097` | Logo and favicon | `L1-024` Brand and design system | [Brand and design-system conformance](platform/brand-and-design-system/README.md) |
| `L2-098` | Design-system conformance | `L1-024` Brand and design system | [Brand and design-system conformance](platform/brand-and-design-system/README.md) |
