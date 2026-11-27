# Detailed designs

Each folder below is one vertically sliced feature of Keryx. Its `README.md` holds the design: an
overview, the components the slice introduces, the level-2 (L2) requirements it realizes, and C4,
class and sequence diagrams rendered from the PlantUML sources in its `diagrams/` folder.

The requirements are in [`docs/specs/`](../specs/L1.md). Diagrams are rendered with
`python3 .claude/skills/writing-software-design-documents/scripts/render_puml.py docs/detailed-designs`.

## Sources

The catalogue of places Keryx collects from, and the rules for fetching them.

| Feature | L2 requirements |
|---|---|
| [Add a source](sources/add-source/README.md) | `L2-001`, `L2-064` |
| [Test a source fetch](sources/test-source-fetch/README.md) | `L2-002`, `L2-063` |
| [Edit and remove a source](sources/edit-and-remove-source/README.md) | `L2-003`, `L2-065` |
| [List sources with their health](sources/list-sources-with-health/README.md) | `L2-004`, `L2-062`, `L2-066` |
| [Fetch respectfully and safely](sources/fetch-respectfully-and-safely/README.md) | `L2-021`, `L2-022`, `L2-023`, `L2-024` |

## Collection

The scheduled and on-demand runs that turn fetched items into scored, deduplicated records.

| Feature | L2 requirements |
|---|---|
| [Schedule collection](collection/schedule-collection/README.md) | `L2-005` |
| [Run collection now](collection/run-collection-now/README.md) | `L2-006`, `L2-047` |
| [Run the collection pipeline](collection/run-collection-pipeline/README.md) | `L2-007`, `L2-008`, `L2-087` |
| [Retry failed sources](collection/retry-failed-sources/README.md) | `L2-008`, `L2-059` |
| [Ingest idempotently](collection/ingest-idempotently/README.md) | `L2-009`, `L2-010`, `L2-011` |
| [Score and publish](collection/score-and-publish/README.md) | `L2-012`, `L2-013` |
| [Record runs and issues](collection/record-runs-and-issues/README.md) | `L2-019`, `L2-020` |
| [Mute an issue](collection/mute-issue/README.md) | `L2-020`, `L2-060` |

## Catalogue

The articles, events and organizations Keryx stores, and how long and how much it keeps.

| Feature | L2 requirements |
|---|---|
| [Store records](catalogue/store-records/README.md) | `L2-015`, `L2-016`, `L2-017`, `L2-018` |
| [Apply retention](catalogue/apply-retention/README.md) | `L2-079` |
| [Keep facts, not copies](catalogue/keep-facts-not-copies/README.md) | `L2-080`, `L2-081` |

## Consumer API

The versioned `/v1` API other products read, and the keys that grant access to it.

| Feature | L2 requirements |
|---|---|
| [Serve articles](consumer-api/serve-articles/README.md) | `L2-026`, `L2-027` |
| [Serve events](consumer-api/serve-events/README.md) | `L2-028` |
| [Serve organizations and sources](consumer-api/serve-organizations-and-sources/README.md) | `L2-029` |
| [Serve reference data and status](consumer-api/serve-reference-and-status/README.md) | `L2-030` |
| [API contract, errors and caching](consumer-api/api-contract-errors-and-caching/README.md) | `L2-025`, `L2-031`, `L2-032`, `L2-086` |
| [Authenticate API clients](consumer-api/authenticate-api-clients/README.md) | `L2-033`, `L2-034`, `L2-038`, `L2-084` |
| [Manage API keys](consumer-api/manage-api-keys/README.md) | `L2-035`, `L2-036`, `L2-068`, `L2-069` |
| [Report API usage](consumer-api/report-api-usage/README.md) | `L2-037`, `L2-067` |

## Admin app

The staff-only Angular app and its cross-cutting interface rules.

| Feature | L2 requirements |
|---|---|
| [Staff sign-in and session](admin-app/staff-sign-in-and-session/README.md) | `L2-039`, `L2-040`, `L2-041` |
| [Admin shell](admin-app/admin-shell/README.md) | `L2-042`, `L2-088`, `L2-097` |
| [Overview](admin-app/overview/README.md) | `L2-043`, `L2-044`, `L2-045`, `L2-046` |
| [Review articles](admin-app/review-articles/README.md) | `L2-048`, `L2-049`, `L2-050`, `L2-051`, `L2-053`, `L2-055` |
| [Decide on an article](admin-app/decide-on-article/README.md) | `L2-014`, `L2-052` |
| [Export articles](admin-app/export-articles/README.md) | `L2-054` |
| [View run history](admin-app/view-run-history/README.md) | `L2-056`, `L2-057`, `L2-058`, `L2-061` |
| [Manage settings](admin-app/manage-settings/README.md) | `L2-070`, `L2-071`, `L2-072` |
| [Page states, dialogs and notifications](admin-app/page-states-dialogs-and-notifications/README.md) | `L2-073`, `L2-074`, `L2-075`, `L2-076` |
| [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) | `L2-089`, `L2-090`, `L2-091`, `L2-092`, `L2-093`, `L2-098` |

## Operations

Alerts, security, audit, telemetry, health and configuration.

| Feature | L2 requirements |
|---|---|
| [Send operational alerts](operations/send-operational-alerts/README.md) | `L2-077`, `L2-078` |
| [Secure transport and input](operations/secure-transport-and-input/README.md) | `L2-082`, `L2-083` |
| [Audit staff actions](operations/audit-staff-actions/README.md) | `L2-085` |
| [Observe the system](operations/observe-the-system/README.md) | `L2-094` |
| [Health, migrations and configuration](operations/health-migrations-and-configuration/README.md) | `L2-095`, `L2-096` |

## Requirement coverage

Every L2 requirement is realized by at least one feature.

| L2 | Feature(s) |
|---|---|
| `L2-001` | [Add a source](sources/add-source/README.md) |
| `L2-002` | [Test a source fetch](sources/test-source-fetch/README.md) |
| `L2-003` | [Edit and remove a source](sources/edit-and-remove-source/README.md) |
| `L2-004` | [List sources with their health](sources/list-sources-with-health/README.md) |
| `L2-005` | [Schedule collection](collection/schedule-collection/README.md) |
| `L2-006` | [Run collection now](collection/run-collection-now/README.md) |
| `L2-007` | [Run the collection pipeline](collection/run-collection-pipeline/README.md) |
| `L2-008` | [Run the collection pipeline](collection/run-collection-pipeline/README.md), [Retry failed sources](collection/retry-failed-sources/README.md) |
| `L2-009` | [Ingest idempotently](collection/ingest-idempotently/README.md) |
| `L2-010` | [Ingest idempotently](collection/ingest-idempotently/README.md) |
| `L2-011` | [Ingest idempotently](collection/ingest-idempotently/README.md) |
| `L2-012` | [Score and publish](collection/score-and-publish/README.md) |
| `L2-013` | [Score and publish](collection/score-and-publish/README.md) |
| `L2-014` | [Decide on an article](admin-app/decide-on-article/README.md) |
| `L2-015` | [Store records](catalogue/store-records/README.md) |
| `L2-016` | [Store records](catalogue/store-records/README.md) |
| `L2-017` | [Store records](catalogue/store-records/README.md) |
| `L2-018` | [Store records](catalogue/store-records/README.md) |
| `L2-019` | [Record runs and issues](collection/record-runs-and-issues/README.md) |
| `L2-020` | [Record runs and issues](collection/record-runs-and-issues/README.md), [Mute an issue](collection/mute-issue/README.md) |
| `L2-021` | [Fetch respectfully and safely](sources/fetch-respectfully-and-safely/README.md) |
| `L2-022` | [Fetch respectfully and safely](sources/fetch-respectfully-and-safely/README.md) |
| `L2-023` | [Fetch respectfully and safely](sources/fetch-respectfully-and-safely/README.md) |
| `L2-024` | [Fetch respectfully and safely](sources/fetch-respectfully-and-safely/README.md) |
| `L2-025` | [API contract, errors and caching](consumer-api/api-contract-errors-and-caching/README.md) |
| `L2-026` | [Serve articles](consumer-api/serve-articles/README.md) |
| `L2-027` | [Serve articles](consumer-api/serve-articles/README.md) |
| `L2-028` | [Serve events](consumer-api/serve-events/README.md) |
| `L2-029` | [Serve organizations and sources](consumer-api/serve-organizations-and-sources/README.md) |
| `L2-030` | [Serve reference data and status](consumer-api/serve-reference-and-status/README.md) |
| `L2-031` | [API contract, errors and caching](consumer-api/api-contract-errors-and-caching/README.md) |
| `L2-032` | [API contract, errors and caching](consumer-api/api-contract-errors-and-caching/README.md) |
| `L2-033` | [Authenticate API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-034` | [Authenticate API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-035` | [Manage API keys](consumer-api/manage-api-keys/README.md) |
| `L2-036` | [Manage API keys](consumer-api/manage-api-keys/README.md) |
| `L2-037` | [Report API usage](consumer-api/report-api-usage/README.md) |
| `L2-038` | [Authenticate API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-039` | [Staff sign-in and session](admin-app/staff-sign-in-and-session/README.md) |
| `L2-040` | [Staff sign-in and session](admin-app/staff-sign-in-and-session/README.md) |
| `L2-041` | [Staff sign-in and session](admin-app/staff-sign-in-and-session/README.md) |
| `L2-042` | [Admin shell](admin-app/admin-shell/README.md) |
| `L2-043` | [Overview](admin-app/overview/README.md) |
| `L2-044` | [Overview](admin-app/overview/README.md) |
| `L2-045` | [Overview](admin-app/overview/README.md) |
| `L2-046` | [Overview](admin-app/overview/README.md) |
| `L2-047` | [Run collection now](collection/run-collection-now/README.md) |
| `L2-048` | [Review articles](admin-app/review-articles/README.md) |
| `L2-049` | [Review articles](admin-app/review-articles/README.md) |
| `L2-050` | [Review articles](admin-app/review-articles/README.md) |
| `L2-051` | [Review articles](admin-app/review-articles/README.md) |
| `L2-052` | [Decide on an article](admin-app/decide-on-article/README.md) |
| `L2-053` | [Review articles](admin-app/review-articles/README.md) |
| `L2-054` | [Export articles](admin-app/export-articles/README.md) |
| `L2-055` | [Review articles](admin-app/review-articles/README.md) |
| `L2-056` | [View run history](admin-app/view-run-history/README.md) |
| `L2-057` | [View run history](admin-app/view-run-history/README.md) |
| `L2-058` | [View run history](admin-app/view-run-history/README.md) |
| `L2-059` | [Retry failed sources](collection/retry-failed-sources/README.md) |
| `L2-060` | [Mute an issue](collection/mute-issue/README.md) |
| `L2-061` | [View run history](admin-app/view-run-history/README.md) |
| `L2-062` | [List sources with their health](sources/list-sources-with-health/README.md) |
| `L2-063` | [Test a source fetch](sources/test-source-fetch/README.md) |
| `L2-064` | [Add a source](sources/add-source/README.md) |
| `L2-065` | [Edit and remove a source](sources/edit-and-remove-source/README.md) |
| `L2-066` | [List sources with their health](sources/list-sources-with-health/README.md) |
| `L2-067` | [Report API usage](consumer-api/report-api-usage/README.md) |
| `L2-068` | [Manage API keys](consumer-api/manage-api-keys/README.md) |
| `L2-069` | [Manage API keys](consumer-api/manage-api-keys/README.md) |
| `L2-070` | [Manage settings](admin-app/manage-settings/README.md) |
| `L2-071` | [Manage settings](admin-app/manage-settings/README.md) |
| `L2-072` | [Manage settings](admin-app/manage-settings/README.md) |
| `L2-073` | [Page states, dialogs and notifications](admin-app/page-states-dialogs-and-notifications/README.md) |
| `L2-074` | [Page states, dialogs and notifications](admin-app/page-states-dialogs-and-notifications/README.md) |
| `L2-075` | [Page states, dialogs and notifications](admin-app/page-states-dialogs-and-notifications/README.md) |
| `L2-076` | [Page states, dialogs and notifications](admin-app/page-states-dialogs-and-notifications/README.md) |
| `L2-077` | [Send operational alerts](operations/send-operational-alerts/README.md) |
| `L2-078` | [Send operational alerts](operations/send-operational-alerts/README.md) |
| `L2-079` | [Apply retention](catalogue/apply-retention/README.md) |
| `L2-080` | [Keep facts, not copies](catalogue/keep-facts-not-copies/README.md) |
| `L2-081` | [Keep facts, not copies](catalogue/keep-facts-not-copies/README.md) |
| `L2-082` | [Secure transport and input](operations/secure-transport-and-input/README.md) |
| `L2-083` | [Secure transport and input](operations/secure-transport-and-input/README.md) |
| `L2-084` | [Authenticate API clients](consumer-api/authenticate-api-clients/README.md) |
| `L2-085` | [Audit staff actions](operations/audit-staff-actions/README.md) |
| `L2-086` | [API contract, errors and caching](consumer-api/api-contract-errors-and-caching/README.md) |
| `L2-087` | [Run the collection pipeline](collection/run-collection-pipeline/README.md) |
| `L2-088` | [Admin shell](admin-app/admin-shell/README.md) |
| `L2-089` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
| `L2-090` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
| `L2-091` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
| `L2-092` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
| `L2-093` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
| `L2-094` | [Observe the system](operations/observe-the-system/README.md) |
| `L2-095` | [Health, migrations and configuration](operations/health-migrations-and-configuration/README.md) |
| `L2-096` | [Health, migrations and configuration](operations/health-migrations-and-configuration/README.md) |
| `L2-097` | [Admin shell](admin-app/admin-shell/README.md) |
| `L2-098` | [Responsive, accessible, themed UI](admin-app/responsive-accessible-themed-ui/README.md) |
