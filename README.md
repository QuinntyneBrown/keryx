<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/design-system/assets/brand/keryx-logo-dark.svg">
    <img src="docs/design-system/assets/brand/keryx-logo.svg" alt="Keryx" width="265" height="80">
  </picture>
</h1>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
</p>

Keryx is a news aggregator for everything Christian and fitness: events, new organizations,
churches and the news around them, primarily in Toronto and the Greater Toronto Area, and
also around the world. It has three parts:

- **Workers:** run daily searches and add or update records in the database, which holds
  everything Keryx knows about Christian and fitness.
- **API:** other products call it directly for information to publish or use on their own
  websites. [Christian Fitness](https://github.com/QuinntyneBrown/christian-fitness) is the
  first of them.
- **Admin web app (staff):** see when each worker last ran and how many records it added or
  updated, and run minor queries of the database. Keryx has no public website of its own.

> **Status:** specified, designed and scaffolded. The requirements
> ([L1](docs/specs/L1.md), [L2](docs/specs/L2.md)), the [detailed designs](docs/detailed-designs/),
> the [mocks](docs/mocks/README.md) of every admin screen and state, and the
> [design system](docs/design-system/README.md) extracted from them are in place. The skeleton
> runs: the Angular workspace with its four projects and a placeholder shell, the .NET solution
> with health endpoints, the `migrate` and `seed` commands, startup validation of settings and
> OpenTelemetry, and CI. No feature is built yet: [docs/journeys.md](docs/journeys.md) lists
> the journeys in order and how far each one is.

## Contents

- [Architecture](#architecture)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [Security](#security)
- [Support](#support)
- [License](#license)

## Architecture

The stack is the same as Christian Fitness.

| Layer     | Technology                                                                                                    |
| --------- | ------------------------------------------------------------------------------------------------------------- |
| Frontend  | Angular (standalone, signals, `OnPush`, zoneless), CDK, Playwright for E2E                                    |
| Backend   | .NET clean architecture: Domain, Application (MediatR), Infrastructure (EF Core), Api (controllers), Worker (scheduled hosted services) |
| Telemetry | OpenTelemetry                                                                                                 |
| CI        | GitHub Actions: lint, build and one critical acceptance path per feature                                      |

## Repository layout

```text
├── frontend/          Angular workspace
│   ├── projects/
│   │   ├── admin/     admin app
│   │   ├── components/ presentation components
│   │   ├── domain/    pure rules and helpers
│   │   └── api/       contracts: DTOs, service interfaces, injection tokens
│   └── e2e/           Playwright specs, page objects and the shared fixture
├── backend/
│   ├── src/           Keryx.{Domain,Application,Infrastructure,Api,Worker,Cli}
│   └── tests/         Keryx.{Application,Api}.Tests
├── tools/             video-record, video-audio, video-build: the slice films; deploy: the release
└── docs/              specs, detailed designs, mocks, design system, ADRs, videos
```

## Getting started

### Prerequisites

- [Node.js](https://nodejs.org/) 24 and npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10 (`backend/global.json` pins the version)
- [PostgreSQL](https://www.postgresql.org/) (the skeleton was checked on 16), only to run the
  API, the worker or the CLI: no test needs it
- Git

### Clone

```sh
git clone https://github.com/QuinntyneBrown/keryx.git
cd keryx
```

### Frontend

```sh
cd frontend
npm ci
npx playwright install chromium   # once, for the end-to-end specs
npm start                         # the admin app
npm run lint                      # ESLint, including the dependency direction
npm run format:check              # Prettier
npm run build                     # failing beyond the angular.json budgets
npm test                          # unit tests of the domain and components libraries
npm run e2e                       # Playwright behaviour specs
```

Presentation components live in `frontend/projects/components/`, one folder per
[design-system component](docs/design-system/components/) under its design-system name, and
the app composes them: build a missing one there first, never in the app.

### Backend

The tests need no database and no network.

```sh
cd backend
dotnet tool restore                            # dotnet-ef, for adding a migration
dotnet build                                   # analysers on, warnings are errors
dotnet test                                    # Application and Api tests, EF Core InMemory
dotnet format --verify-no-changes              # the format check CI runs
```

To run the processes, set `ConnectionStrings__Keryx` to a PostgreSQL connection string. The API
also requires the staff identity provider settings `Keryx__Oidc__Authority` and `Keryx__Oidc__ClientId`. Every
setting is in [tools/deploy/configuration.md](tools/deploy/configuration.md); a missing
required one stops a process at startup, naming it.

```sh
dotnet run --project src/Keryx.Cli -- migrate  # the API never migrates on startup
dotnet run --project src/Keryx.Cli -- seed     # regions, categories, default settings; safe to repeat
dotnet run --project src/Keryx.Api             # the API other products call
dotnet run --project src/Keryx.Worker          # the daily searches
```

`GET /health/live` answers when the API runs and `GET /health/ready` when its database is at
the schema this build expects. A schema change is a new migration:

```sh
dotnet ef migrations add <Name> --project src/Keryx.Infrastructure --startup-project src/Keryx.Infrastructure --output-dir Persistence/Migrations
```

## Documentation

| Path                     | Contents                                     |
| ------------------------ | -------------------------------------------- |
| `docs/specs/`            | L1 and L2 requirements                       |
| `docs/detailed-designs/` | Per-feature software design documents        |
| `docs/mocks/`            | HTML mocks for every admin screen and state  |
| `docs/design-system/`    | Tokens, foundations, components and patterns; the logo and favicon in `assets/brand/` |
| `docs/adr/`              | Architecture decision records                |
| `docs/journeys.md`       | The journeys in build order, with effort and how complete each is |
| `docs/estimates/`        | Effort and percent complete per detailed design, and a brief roadmap |
| `docs/lessons-learned.md` | What each journey taught; read before starting one |
| `tools/deploy/`          | What a release runs and every setting a deployment needs |
| `AGENTS.md`              | Engineering rules for people and AI agents   |
| `.claude/skills/`, `.agents/skills/` | Agent skills: requirements, designs, mocks, design system, incremental implementation |

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue
or pull request. Everyone taking part is expected to follow the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please do not report security vulnerabilities through public GitHub issues. See
[SECURITY.md](SECURITY.md).

## Support

See [SUPPORT.md](SUPPORT.md) for how to get help.

## License

Copyright (c) 2026 Quinntyne Brown. Licensed under the [MIT License](LICENSE).
