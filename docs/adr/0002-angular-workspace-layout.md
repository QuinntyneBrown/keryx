# 0002. Angular workspace layout: one app and three source libraries

## Status

Accepted, 27 November 2026 (journey 1, scaffolding).

## Context

`AGENTS.md` mandates `frontend/projects/` with the `admin` app and the `components`, `domain`
and `api` libraries, each lazy-loadable on its own. The Angular CLI generates a library as a
package that is built with ng-packagr into `dist/` and imported from there. That is the right
shape for a library published to a registry. These three are never published: they exist to
draw the dependency direction and to keep presentation, rules and contracts apart.

## Decision

The libraries are **source libraries**. The app imports them through TypeScript path aliases
that point at each library's `src/public-api.ts`:

| Alias | Library | Holds |
|---|---|---|
| `@kx/components` | `projects/components` | Every design-system component, built once under its design-system name |
| `@kx/domain` | `projects/domain` | Pure rules and helpers, with unit tests |
| `@kx/api` | `projects/api` | DTO types, service interfaces and injection tokens |

- No library has a `build` target and nothing is built with ng-packagr. The application
  builder compiles what the app imports, tree-shakes the rest and splits lazy routes as usual.
- `components` and `domain` have a `test` target (`@angular/build:unit-test`, Vitest, happy-dom).
  `api` holds types and tokens only and has no tests.
- The selector prefix is `kx` for every project; a component's class and file omit it
  (`Brand` in `brand/brand.ts`, selector `kx-brand`).
- The favicon stays at `frontend/public/favicon.ico` (L2-097), so the app's assets come from
  `frontend/public/`.
- The initial budget is 250 kB of uncompressed output and 8 kB for any component style
  (L2-088). The warnings sit just below (240 kB and 6 kB) so that growth is seen before it fails.
- `outputHashing` is `bundles`: scripts and styles carry a content hash, media files do not.
  `index.html` can then preload the one font used above the fold by a fixed address,
  `media/Geist-Variable.woff2` (L2-088). A font file changes only with the design system.
- The design system's switch points are mirrored for media queries in one Sass partial,
  `projects/components/src/_breakpoints.scss` (`from(1024)`, `until(1024)`). It lives in the
  components library because that is where layout is drawn and a library cannot import the app.

This departs from the CLI default (`ng generate library` with a build step) for the reason
above.

## Consequences

- One compilation, no `dist/` hand-off between projects, and a change in a library is seen by
  the app's dev server at once.
- The libraries cannot be published as they are. If Keryx ever needs to share one with another
  repository, that library gets a build target and a new ADR.
- Because a path alias would let any project import any other, the dependency direction is
  enforced by lint ([ADR 0003](0003-eslint-boundaries-for-the-dependency-direction.md)).
- A second application needs an ADR (`AGENTS.md`): Keryx has an admin app only.
