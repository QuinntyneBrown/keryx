<h1 align="center">Keryx</h1>

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

> **Status:** just started. This repository holds the engineering rules and agent skills;
> requirements, designs, mocks and application code (`frontend/`, `backend/`) come next, in
> incremental slices. The layout and commands below describe where the code will live.

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

- [Node.js](https://nodejs.org/) LTS and npm
- [.NET SDK](https://dotnet.microsoft.com/download) (current LTS)
- Git

### Clone

```sh
git clone https://github.com/QuinntyneBrown/keryx.git
cd keryx
```

### Frontend

Once the workspace exists:

```sh
cd frontend
npm ci
npx playwright install chromium   # once, for the end-to-end specs
npm start                         # the admin app
npm run lint                      # ESLint, including the dependency direction
npm run format:check              # Prettier
npm run build                     # failing beyond the angular.json budgets
npm run e2e                       # Playwright behaviour specs
```

### Backend

Once the solution exists. The tests need no database and no network.

```sh
cd backend
dotnet build                                   # analysers on, warnings are errors
dotnet test                                    # Application and Api tests, EF Core InMemory
dotnet run --project src/Keryx.Cli -- migrate  # the API never migrates on startup
dotnet run --project src/Keryx.Api             # the API other products call
dotnet run --project src/Keryx.Worker          # the daily searches
```

## Documentation

| Path                     | Contents                                     |
| ------------------------ | -------------------------------------------- |
| `docs/specs/`            | L1 and L2 requirements                       |
| `docs/detailed-designs/` | Per-feature software design documents        |
| `docs/mocks/`            | HTML mocks for every admin screen and state  |
| `docs/design-system/`    | Tokens, foundations, components and patterns |
| `docs/adr/`              | Architecture decision records                |
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
