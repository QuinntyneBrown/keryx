# 0006. The shell routes its screens, and each navigation link arrives with its screen

## Status

Accepted, 27 November 2026 (journey 2, staff signs in). Supersedes [ADR 0004](0004-shell-placeholder-until-the-admin-shell.md).

## Context

ADR 0004 put a placeholder card in the shell until journey 2 and redirected every unknown address
to it. Journey 2 now needs the Not found screen (L2-040 AC4) and the signed-in person in the
sidebar (L2-039 AC5). L2-042 asks for six navigation links, but five of their screens (Overview,
Articles, Runs, Sources, API) are built by later journeys. A link to a screen that does not
exist would lead every press to Not found.

The mocks draw a navigating action as `<a class="btn" role="button">`. A control that changes the
address is a link to assistive technology and to the browser (open in a new tab, copy link).
WCAG and the ARIA practices say not to give it the button role.

## Decision

- The shell is a layout route: the guarded route loads `shell/shell.routes.ts`, whose one route
  has `AppShell` as its component. Its children render in its page body: `''` is the stand-in
  `HomePlaceholder` (ADR 0004's card, unchanged) until the Overview screen replaces it, and `**`
  is the Not found screen. Both load lazily.
- The navigation is not announced until it has a link. A screen's navigation link and its
  `aria-current` state are added in the same journey that builds the screen. Settings is the
  first, with its Account section, in journey 2.
- `kx-btn` with an `href` renders `<a class="btn …">` without `role="button"`. A plain click
  emits `navigate` for the app's router, the same as `kx-brand`. The mocks keep their markup:
  the BEM classes, which e2e locators depend on, are the same.
- The Not found screen shows "Go to overview" only. The mock's second action, "Browse articles",
  is added when the Articles screen exists (journey 11).

## Consequences

- An unknown address keeps its URL and shows Not found inside the shell. A signed-out visitor is
  still sent to Sign in first, because the guard is on the shell route.
- The shell spec no longer covers ADR 0004's redirect. `not-found.spec.ts` covers its
  replacement.
- The initial total went from 223.82 kB to 227.05 kB; the budget stays at 250 kB. A
  `--stats-json` diff shows Angular core's rendering runtime (`_debug_node`), shared by the
  new lazy screens, moving into `main`. About 2 kB of that is the build's chunk optimiser
  folding small shared chunks into `main` (`NG_BUILD_OPTIMIZE_CHUNKS=0` gives 225.04 kB at the
  cost of more requests). Every signed-in visit loads this code right after `main`, so no
  visit downloads more.
- Until each journey lands, the shell shows fewer links than the mock's six. L2-042 AC1 is met
  when the last of those screens is built.
