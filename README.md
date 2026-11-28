<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/design-system/assets/brand/keryx-logo-dark.svg">
    <img src="docs/design-system/assets/brand/keryx-logo.svg" alt="Keryx" width="265" height="80">
  </picture>
</h1>

Keryx collects news and events from churches, fitness groups, and other organizations.
Coverage starts in Toronto and the Greater Toronto Area and extends worldwide. Products
read published records through a versioned API. Staff manage sources and review what gets
published in a separate admin app.

## How it works

1. The collection worker fetches sources on a schedule, merges duplicate records, and tracks
   where each item came from. A relevance score determines which articles are published,
   held for review, or hidden.
2. Staff use the Angular admin app to review publication decisions, maintain sources, and
   monitor collection runs. They can start a run on demand or retry the sources that failed.
3. Products use scoped API keys to read published articles, events, organizations, sources,
   categories, regions, and collection status.

See the [journey tracker](docs/journeys.md) for implementation status.

## Use the API

Ask a Keryx staff member for the API base URL and a key with the `articles:read` scope.
Set `KERYX_API_URL` and `KERYX_API_KEY` in your shell, then request the five most recent
published articles.

```sh
curl --fail-with-body \
  --header "Authorization: Bearer ${KERYX_API_KEY}" \
  "${KERYX_API_URL}/v1/articles?limit=5"
```

If the response includes a nonempty `next_cursor`, pass it as the `cursor` query parameter
to read the next page. The running API serves its OpenAPI document at `/openapi/v1.json`.

## Develop locally

Clone the repository. Start each set of commands below from its root.

```sh
git clone https://github.com/QuinntyneBrown/keryx.git
cd keryx
```

### Build and test

For the backend, install the .NET 10 SDK specified in [global.json](backend/global.json).
The tests use an in-memory database and fake sources.

```sh
cd backend
dotnet build
dotnet test --no-build
```

For the admin app, install Node.js 24 and npm. Playwright starts the app and supplies API
responses, so you can exercise staff workflows without configuring a database or identity
provider.

```sh
cd frontend
npm ci
npm run build
npm test
npx playwright install chromium
npm run e2e
```

### Run the services

To run the backend against PostgreSQL, set the following environment variables in the
shells that will launch each process. The [configuration guide](tools/deploy/configuration.md)
covers connection strings, identity provider registration, and optional settings.

| Environment variable | Used by | Value |
| --- | --- | --- |
| `ConnectionStrings__Keryx` | CLI, API, worker | PostgreSQL connection string |
| `Keryx__Fetch__UserAgentContactUrl` | API, worker | HTTPS page identifying the collector and its operator |
| `Keryx__Oidc__Authority` | API | HTTPS issuer URL for staff sign-in |
| `Keryx__Oidc__ClientId` | API | Client ID registered with the identity provider |

Apply migrations and seed the database before starting the API. Both CLI commands are safe
to repeat.

```sh
cd backend
dotnet run --project src/Keryx.Cli -- migrate
dotnet run --project src/Keryx.Cli -- seed
dotnet run --project src/Keryx.Api
```

Request `GET /health/ready` at the API's listening address. A `200 Healthy` response means
the API can reach the database and the schema matches the build. To start scheduled collection,
run the worker in a separate terminal.

```sh
cd backend
dotnet run --project src/Keryx.Worker
```

To start the Angular development server, run `npm start` from `frontend/`. For live staff
sign-in, serve the app and its `/admin/v1/` API routes under the same HTTPS origin and
register the callback URL ending in `/admin/v1/auth/callback` with the identity provider.

## Documentation

| To learn about | Start here |
| --- | --- |
| Product scope and delivery progress | [Requirements](docs/specs/L1.md) · [User journeys](docs/journeys.md) |
| System architecture and design decisions | [Detailed designs](docs/detailed-designs/) · [Architecture decision records](docs/adr/) |
| Admin app structure and interface | [Frontend guide](frontend/README.md) · [Design system](docs/design-system/README.md) · [Screen mocks](docs/mocks/README.md) |
| Deployment, health checks, and telemetry | [Configuration guide](tools/deploy/configuration.md) |

## Contributing and support

For development conventions and the pull request workflow, see the [contributing guide](CONTRIBUTING.md)
and [code of conduct](CODE_OF_CONDUCT.md). For help, see [support](SUPPORT.md).
Report vulnerabilities privately through the [security policy](SECURITY.md).

## License

Keryx is licensed under the [MIT License](LICENSE).
