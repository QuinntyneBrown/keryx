# Observe the system

## Overview

Keryx runs unattended: workers collect on a schedule, and other products call the API at any
time. The people who run it need to see what happened, how long it took and where it failed,
without that view leaking anything private. This cross-cutting feature makes the Keryx API and
the Keryx Worker observable through OpenTelemetry. It has no screen of its own; the admin app
shows only the `trace_id` of an error, as its "Reference".

- **OpenTelemetry** — vendor-neutral standard and SDK for traces, metrics and logs
- **OTLP** — OpenTelemetry protocol used to export telemetry to a collector
- **span** — timed unit of work in a trace, such as one HTTP request, one run stage or one source fetch
- **trace** — tree of spans that shares one trace id
- **route template** — route as declared, with placeholders (`/v1/articles/{id}`), never the requested path or its query
- **redaction** — removal of a value from telemetry before it is exported

Both processes export traces, metrics and logs over OTLP to the configured endpoint. With no
endpoint configured they run without error and export nothing. A request span records the
route template, method, status and duration and nothing else that could identify a person or a
secret. A collection run is one trace, with a span per stage and per source fetch; a fetch span
names the source by id, never by its URL's query. Every log line is structured and carries no
fetched content, summary, connection string or secret. An error response carries the trace's id
as `trace_id`.

## Description

**Shared (`Keryx.Infrastructure`)**

- **`KeryxTelemetry.AddKeryxTelemetry(IServiceCollection, IConfiguration, string defaultServiceName, configureTracing, configureMetrics)`**
  — extension called by the API and the worker. It registers the OpenTelemetry SDK for traces,
  metrics and logs under one service name (`OTEL_SERVICE_NAME`, else the default the caller
  gives: `keryx-api` or `keryx-worker`), with the activity source and meters below, and the OpenTelemetry logging provider. It adds the OTLP exporter
  only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; with none, nothing is exported and the process
  runs the same. Sampling and export intervals are the SDK's defaults (every trace sampled,
  spans and logs exported in batches every 5 seconds, metrics every 60 seconds); each can be
  changed through the standard `OTEL_*` variables without a build. The API adds the ASP.NET
  Core instrumentation through the two callbacks (`configureTracing`, `configureMetrics`), so
  `Keryx.Infrastructure` and the worker carry no web dependency. The redaction processor is
  registered after the caller's callback, so no caller can leave it out.
- **`TelemetryRedactionProcessor`** — `BaseProcessor<Activity>` that runs before export. It
  keeps an allow-list of tags (`http.route`, `http.request.method`,
  `http.response.status_code`, `keryx.source.id`, `keryx.run.id`, `keryx.stage`,
  `keryx.client.id`, `error.type`) and removes every other tag, including `url.query`,
  `url.full`, `url.path`, header values, `enduser.*` and exception messages; it also clears the
  span's status description, which may hold an exception's message. Each `keryx.*` tag joins
  the list in the change that first sets it.
- **Request logs** — the API filters the `Microsoft.AspNetCore` log categories to `Warning`,
  because their information lines print each request's full URL with its query string. Those
  lines are neither written to the console nor exported.
- **No `HttpClient` instrumentation.** The source fetcher drives its own `SocketsHttpHandler`
  through `ISourceFetcher`, so the `source.fetch` span above is the fetch's span. It is named by
  the source's id, so no URL can reach it. A client span per attempt would carry only a method
  and a status after redaction, and adds no answer that the run's stage record lacks.
- **`LogRedaction`** — the logging rules. Log calls use source-generated `LoggerMessage`
  methods whose parameters are ids, counts, kinds and durations only. Scopes and state that
  carry request data are not logged. Exceptions are logged by type and correlation id, never by
  message, where the message may hold fetched text.

**API (`Keryx.Api`)**

- **ASP.NET Core instrumentation** — records `http.route` from the endpoint's route template,
  method, status and duration, on the span and on the histogram `http.server.request.duration`
  (`s`). Its count is the number of requests, its buckets are the latency, and its
  `http.response.status_code` and `error.type` tags are the errors.
- **Client on consumer requests** — `ApiUsageMiddleware`, which already reads the authenticated
  client of each `/v1` request, adds `keryx.client.id` (the client's public id, never its key or
  prefix) to the request's measurement through `IHttpMetricsTagsFeature`, and to its span. A
  request with no valid key carries no client. The design first had a separate `ApiMetrics`
  meter with `keryx.api.requests`, `keryx.api.request.duration` and `keryx.api.errors`. That would
  repeat the framework's own instrument under other names. Enriching it is the way ASP.NET Core
  documents, and it gives the same three measures by route template and client.
- **`ProblemDetailsMapper`** — adds `trace_id` from `Activity.Current.TraceId` to every
  ProblemDetails body (`L2-031`).

**Worker (`Keryx.Worker`)**

- **`RunTelemetry`** (`Keryx.Application`) — activity source `Keryx.Runs`, used by every worker
  run. `RunRecordingBehavior` starts the root span of each run, named `<worker>.run`
  (`collection.run`, `retention.run`, `alert_digest.run`). The run's handler tags it with
  `keryx.run.id` once it knows the run. A collection pickup that finds no run to execute
  clears the span's recorded flag, so the pickup every 10 seconds exports nothing. A run that
  throws ends its span with status `Error` and `error.type`, never a message. The run's
  correlation id is the trace's id, so the admin app's reference leads to the trace.
  `CollectionPipeline` adds a child span per stage, `collection.<stage>`, tagged `keryx.stage`.
  The fetch, parse, classify, deduplicate and store stages stream together, so their spans open
  together and close when the stream ends; each stage's busy time is in the run's stage record.
  Publish follows on its own. Under the fetch stage there is a span per source fetch,
  `source.fetch`, tagged with `keryx.source.id` only. The source is static, as .NET advises for
  an `ActivitySource`; the Application layer needs nothing beyond `System.Diagnostics` for it.
- **`RunMetrics`** (`Keryx.Application`) — meter `Keryx.Runs`, made by `IMeterFactory`:
  - histogram `keryx.run.duration` (`s`), tagged `keryx.run.outcome`;
  - counter `keryx.run.items` (`{item}`), tagged `result` = `added`, `updated` or `unchanged`;
  - counter `keryx.run.sources_failed` (`{source}`);
  - histogram `keryx.fetch.duration` (`s`), tagged `keryx.source.id`.

  Durations are in seconds, as the OpenTelemetry conventions measure them
  (`http.server.request.duration` is too). No convention covers a run, so the names keep the
  `keryx.` namespace. A run's measures are recorded when its handler has saved it as finished; a
  run that throws is counted by the failure alert and its trace, not here. A fetch is measured
  whatever its result. Tagging by source id is bounded by the catalogue, which staff curate.

**Admin app (`frontend/projects`)**

- **`ApiError`** (`api` library) — the error DTO carries `code` and `trace_id`. The HTTP
  implementations map a ProblemDetails body to it.
- **`alert`** component (`components` library) — shows the generic message and "Reference"
  followed by the `trace_id` as text, as the screens' error states require (`L2-073`).

**Configuration**

- `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_SERVICE_NAME` (defaults
  `keryx-api` and `keryx-worker`) and `OTEL_RESOURCE_ATTRIBUTES` are documented in
  `tools/deploy/` (health, migrations and configuration feature). The OTLP protocol default is
  the SDK's, `grpc`; `http/protobuf` is chosen with `OTEL_EXPORTER_OTLP_PROTOCOL`.

**Delivered in steps**

- Journey 1 (scaffolding) builds the wiring both processes share: `AddKeryxTelemetry`, the
  ASP.NET Core instrumentation, the redaction processor with the four tags a request span
  carries today (`http.route`, `http.request.method`, `http.response.status_code`,
  `error.type`), the request-log filter and `trace_id` on every ProblemDetails body
  (`L2-094` criteria 1 and 5, and the span half of criterion 2).
- Journey 25 adds the run traces (`RunTelemetry`), the run metrics, the consumer API's metrics
  by client, and the log rules.
- Journey 25 completes the feature: the run traces and run metrics, the `LogRedaction` rules
  for exceptions, and the acceptance tests of criteria 3, 4 and 6.

## Requirements

The feature realizes the following level-2 (L2) requirements. Each L2 requirement refines a
level-1 (L1) requirement, cited by identifier. Their acceptance criteria are in
[L2.md](../../../specs/L2.md).

| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-094` | `L1-023` | Telemetry — The API and the worker are observable through OpenTelemetry, and no telemetry carries a secret, a query string, a body or personal data. |

## Diagrams

### System context

Keryx exports its telemetry to the OTLP collector. Staff and consuming products generate the
requests it describes.

![C4 system context for observing the system](diagrams/c4-context.png)

### Containers

The Keryx API and the Keryx Worker each export traces, metrics and logs. The Keryx Admin app
reads only the `trace_id` returned in an error.

![C4 container view for observing the system](diagrams/c4-container.png)

### Components

Instrumentation and the API and collection meters feed the SDK; `TelemetryRedactionProcessor`
removes every tag outside the allow-list before the OTLP exporter sends anything.

![C4 component view for observing the system](diagrams/c4-component.png)

### Class structure

`KeryxTelemetry` wires the sources, meters, processor and exporter that both processes share.

![Class diagram for observing the system](diagrams/class-structure.png)

### Behaviour — configure telemetry at startup

Each process registers OpenTelemetry and adds the OTLP exporter only when an endpoint is
configured.

![Sequence diagram for configuring telemetry](diagrams/sequence-configure-telemetry.png)

### Behaviour — trace an API request

A request span records the route template, method, status and duration. An error carries the
trace id as `trace_id`, which the admin app shows as the "Reference".

![Sequence diagram for tracing an API request](diagrams/sequence-trace-api-request.png)

### Behaviour — trace a collection run

One trace covers the run, with a span per stage and per source fetch, and the run's counts are
recorded as metrics.

![Sequence diagram for tracing a collection run](diagrams/sequence-trace-collection-run.png)
