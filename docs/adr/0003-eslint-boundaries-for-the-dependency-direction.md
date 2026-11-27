# 0003. eslint-plugin-boundaries enforces the dependency direction

## Status

Accepted, 27 November 2026 (journey 1, scaffolding).

## Context

`AGENTS.md` says libraries depend one way and never import an app, presentation components
take inputs and emit outputs only (no app service, token or router), and ESLint enforces the
boundaries. The workspace is a plain Angular CLI workspace, not Nx, so
`@nx/enforce-module-boundaries` is not available, and `no-restricted-imports` cannot tell a
relative import into another project from one inside the same project. The design of the
responsive, accessible, themed UI listed the lint toolchain as "Prettier and angular-eslint
only" and left any addition to an ADR.

## Decision

Use **eslint-plugin-boundaries** (rule `boundaries/dependencies`) in
`frontend/eslint.config.js`, resolving the `@kx/*` aliases with
`eslint-import-resolver-typescript`:

| From | May import |
|---|---|
| `admin` (type `app`) | `components`, `domain`, `api` |
| `components` | `api`, **type imports only** (`import type`): no token, service or other value |
| `domain` | `api` |
| `api` | nothing in the workspace |

The default is `disallow`, so a library importing the app is an error. Imports inside one
project are not checked. A new library is added to `boundaries/elements` and to this table in
the same change.

A second rule keeps presentation components free of the framework's services: in
`projects/components`, `no-restricted-imports` refuses `@angular/router` and
`@angular/common/http`. A component that must send someone somewhere renders a real `<a href>`
and emits a `navigate` output; the app decides what navigating means.

The same config enforces, through angular-eslint: the `kx` selector prefix, no inline
`template` or `styles`, no `style` attribute in a template, no `ViewEncapsulation.None`, and the
template accessibility rules. Prettier owns format (`eslint-config-prettier` comes last).

These are lint rules, not tests: the repository forbids architecture tests.

## Consequences

- `npm run lint` fails on a wrong-way import and names both element types. In journey 1 a
  throwaway file for each forbidden direction failed (a library importing the app, `api`
  importing `domain`, a value import from `api` in `components`, `@angular/router` in
  `components`) while the allowed imports passed; the throwaway files were not committed.
- Type-only imports from `api` keep DTO types available to presentation components while
  tokens and services stay out of them.
- A stylesheet lint for raw hex colours and durations is still not in the toolchain. Adding one
  needs its own ADR.
