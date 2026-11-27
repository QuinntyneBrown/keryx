# 0004. A shell placeholder stands in until the admin shell is built

## Status

Accepted, 27 November 2026 (journey 1, scaffolding). Superseded when journey 2 builds the admin
shell of L2-042 and the Not found screen of L2-040.

## Context

Journey 1 creates the workspace with no feature screen, yet it must prove that the app builds,
loads, renders the design system and shows the Keryx mark as the shell's home link (L2-097).
No mock exists for an app with no screens: every mock of the shell shows six navigation links,
the next collection and the signed-in person, and all of that arrives with later journeys.
`AGENTS.md` says to add a missing mock state or record a stand-in in an ADR rather than invent,
and L2-098 says every piece of markup comes from a design-system component.

A mock for a screen that journey 2 deletes would be waste, so this ADR records a stand-in.

## Decision

The route `''` lazily loads **`AppShell`** (`frontend/projects/admin/src/app/shell/`), a
placeholder that is the real shell's frame with nothing in it yet. It contains no markup of its
own: it composes five design-system components from the components library, each built in
full from its design-system page, so journey 2 extends the shell instead of replacing it.

| Component | Library folder | In the placeholder |
|---|---|---|
| `skip-link` | `components/src/skip-link/` | "Skip to content", the first focusable element |
| `app` | `components/src/app/` | The frame: sidebar slot and `<main id="main">`; `page-body` centred |
| `sidebar` | `components/src/sidebar/` | Head only: the lockup and the "Admin" tag; no links, no footer |
| `brand` | `components/src/brand/` | The mark and wordmark, one link named "Keryx, go to overview" |
| `empty` | `components/src/empty/` | A card whose title is the page's `h1` |

The sidebar is told it has no navigation (`[navigation]="false"`), so no empty landmark is
announced; the navigation landmark of L2-092 criterion 7 arrives with the links in journey 2.

The copy is a stand-in and is not a designed state: title "Keryx Admin", text "The admin screens
are not built yet." The document title is "Admin · Keryx". Any other address redirects to `''`
until the Not found screen exists.

This sets the precedent every later screen follows: **a page or dialog in the app orchestrates
and composes; everything that is drawn is a component in `frontend/projects/components/`,
built once under its design-system name and reused from there.**

## Consequences

- The smoke spec (`frontend/e2e/admin/shell.spec.ts`) proves the frame, the mark and the lazy
  load through the same page object journey 2 will extend.
- The placeholder's copy has no mock, so the visual sweep has nothing to compare it with;
  the components themselves are compared with their design-system pages.
- Journey 2 replaces the `empty` card with the routed screens, adds `nav-link` and `avatar`,
  and replaces the redirect with the Not found screen. This ADR is then superseded.
