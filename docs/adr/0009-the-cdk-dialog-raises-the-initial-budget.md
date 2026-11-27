# 0009. The CDK dialog raises the initial budget to 260 kB

## Status

Accepted, 27 November 2026 (journey 3, staff adds a source).

## Context

"Add a source" is the first dialog. AGENTS.md says editing opens a CDK Dialog, and L2-064 AC1
says it is a CDK dialog: the CDK gives the modal role, the focus trap, Escape, the backdrop and
focus restored on close, which a hand-rolled modal would have to repeat.

The CDK's own code loads only when "Add source" is first pressed: `Sources` imports
`@angular/cdk/dialog` and the dialog component dynamically. Even so, the initial total went
from 235.33 kB to 253.40 kB, over the 250 kB budget. Diffing `--stats-json` inputs against the
base shows why: 13.8 kB of Angular core's shared runtime (`_debug_node`), about 4 kB of RxJS
schedulers and operators (`AsyncAction`, `debounceTime`, `auditTime`) that the overlay uses,
and 1.9 kB of the CDK's overlay styles. esbuild does not split one module between chunks, so
the parts of `@angular/core` and RxJS that any lazy code uses land in the chunk `main` already
imports. Without the CDK, the dialog's components cost 2.8 kB.

## Decision

- Use the CDK dialog, loaded when first opened.
- Raise the initial budget from 240 kB / 250 kB (warning / error) to 255 kB / 260 kB.
- The CDK's overlay styles are a foundation (`foundations/_overlay.scss`), with the backdrop's
  colour from `--color-bg-backdrop`.

## Consequences

- Every later dialog (edit and remove source, new and revoke API key, add keyword) reuses what
  this one paid for.
- The budget has about 6.6 kB of room. A later screen that needs more states its reason in its
  own ADR, as this one does.
