# Contributing to Keryx

Thank you for your interest in contributing. This document explains how to report issues,
propose changes and get a pull request merged.

By participating you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report a bug:** open a GitHub issue with steps to reproduce, the expected and actual
  behaviour, and the endpoint, worker or screen involved.
- **Report wrong or missing data:** open an issue naming the event, organization or church,
  what is wrong, and a public source for the correct information.
- **Suggest a feature or a source:** open an issue describing the problem it solves for the
  products that use the API or for staff. Large changes should be discussed before any code
  is written.
- **Improve the docs:** requirements, designs, mocks and the design system live in `docs/`.
- **Submit code:** see the workflow below.

## Before you start

Read [AGENTS.md](AGENTS.md). It is the single source of engineering rules for this
repository (folder structure, frontend and backend conventions, workers and ingestion,
testing, lazy loading and memory). A departure from it needs an architecture decision record
in `docs/adr/`.

## Development workflow

Every change to product code (the API, the workers, the admin app) needs, in this order:

1. A **requirement** in `docs/specs/`.
2. A **detailed design** in `docs/detailed-designs/`.
3. A **mock** in `docs/mocks/` for any visible change to the admin app.

Then implement in small slices using acceptance-test-driven development:

1. Write Given-When-Then acceptance criteria for the slice.
2. Write the acceptance test (backend: through the API; frontend: Playwright with page
   objects) and watch it fail for the expected reason.
3. Implement only that slice.
4. Refactor while green and run the regression checks.

Documentation, mocks and test-only changes are exempt from this flow.

## Pull requests

1. Fork the repository and create a branch from `main`
   (for example `feat/event-search-worker` or `fix/duplicate-churches`).
2. Keep each pull request focused on one slice or concern.
3. Make sure lint, build and tests pass locally:

   ```sh
   cd frontend && npm ci && npm run lint && npm run format:check && npm run build && npm test && npm run e2e
   cd backend && dotnet build && dotnet test
   ```

4. Write clear commit messages in the imperative mood (`Add the run history endpoint`).
5. Open the pull request against `main`, link the related issue and describe what changed
   and how you verified it. Say so plainly if the change alters the API other products call.
6. CI must be green. A maintainer reviews every pull request before merging.

## Continuous integration

CI runs on every push to `main` and every pull request against it: lint, format check and
build for the frontend with one critical Playwright path per feature, and build and
acceptance tests for the backend.

- Nothing is retried. A flaky or slow check is fixed or removed, never re-run until it passes.
- A whole run stays under five minutes.
- CI needs no secret, no backend, no database and no network source: the specs answer every
  request through the shared fixture, the backend tests run on EF Core InMemory, and the
  workers' sources are fakes.

## Coding standards

- Prettier and angular-eslint own frontend formatting and linting; CI fails on either.
- Backend follows clean architecture; business rules live in handlers and domain services.
- The API is a contract other products depend on: version it, never break it in place.
- Ingestion is idempotent, every record keeps its source, and every worker run is recorded.
- Respect each source: `robots.txt`, terms of use and rate limits.
- Meet WCAG 2.2 AA in the admin app: every interaction works by touch and keyboard, and
  colour is never the only carrier of a state.
- Never log contact details, tokens or API keys.

## Licensing

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE).
