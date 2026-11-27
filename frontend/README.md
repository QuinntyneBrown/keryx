# Keryx frontend

One Angular workspace: the admin app and three source libraries
([ADR 0002](../docs/adr/0002-angular-workspace-layout.md)).

| Project               | Alias            | Holds                                                                  |
| --------------------- | ---------------- | ---------------------------------------------------------------------- |
| `projects/admin`      |                  | The admin app: `app.config.ts`, routes, pages and dialogs              |
| `projects/components` | `@kx/components` | Every design-system component, built once under its design-system name |
| `projects/domain`     | `@kx/domain`     | Pure rules and helpers, with unit tests                                |
| `projects/api`        | `@kx/api`        | Contracts: DTO types, service interfaces and injection tokens          |

The app uses the three libraries; `components` and `domain` use only `api`; no library imports
the app. ESLint fails on any other import
([ADR 0003](../docs/adr/0003-eslint-boundaries-for-the-dependency-direction.md)).

## Presentation components live in `projects/components`

Everything a person sees is drawn by a component of the components library. The app's pages
and dialogs orchestrate: they hold the services, the router and the state, and they compose
library components. They draw nothing themselves. `projects/admin/src/app/shell/` is the
example to follow: its template is library components only, and its stylesheet is one line.

To put something on a screen:

1. Find its page in [`docs/design-system/components/`](../docs/design-system/components/).
2. If `projects/components/src/<name>/` exists, import it from `@kx/components` and use it.
3. If it does not, build it there first, under the design-system name, with that page's block
   name, anatomy, variants, states and tokens: `<name>.ts`, `<name>.html`, `<name>.scss` and
   `<name>.spec.ts`. Export it from `projects/components/src/public-api.ts`. Then use it.
4. Never write a second component for the same thing, under any name, and never build one in
   the app. A composite with no design-system page is built from library components.

A library component takes inputs and emits outputs. It injects no app service, no token and no
router, and it calls no API: one that sends a person somewhere renders a link and emits
`navigate`, and the app decides what that means. ESLint refuses `@angular/router` and
`@angular/common/http` in the library.

## Commands

```sh
npm ci
npx playwright install chromium   # once, for the end-to-end specs
npm start                         # the admin app, compiled as you edit
npm run lint                      # ESLint, including the dependency direction
npm run format:check              # Prettier
npm run build                     # fails beyond the angular.json budgets
npm test                          # unit tests of the domain and components libraries
npm run e2e                       # every Playwright behaviour spec
npm run e2e:critical              # the one spec per feature that CI runs
```

The specs need no backend: the shared fixture in `e2e/fixtures/` answers every API request and
fails a spec on an unanswered request, a request off localhost or a page error. One page object
per screen or dialog, in `e2e/page-objects/`, owns the selectors.

## The favicon

`public/favicon.ico` is the Keryx mark (16, 24, 32, 48, 64, 128 and 256 px), generated from
[`docs/design-system/assets/brand/keryx-mark.svg`](../docs/design-system/assets/brand/keryx-mark.svg).
Keep this file: do not let a tool's default icon replace it (requirement L2-097).
