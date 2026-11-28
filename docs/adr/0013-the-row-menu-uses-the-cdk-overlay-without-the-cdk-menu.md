# 0013. The row menu uses the CDK overlay without the CDK Menu

## Status

Accepted, 28 November 2026 (journey 17, staff maintain sources).

## Context

The design system's menu page says "Build it on the CDK Menu". L2-063 asks for the WAI-ARIA
menu button pattern: focus on the first item when it opens, the arrow keys, Home and End moving
and wrapping, Escape and focus loss closing it and returning focus, one menu open at a time,
and the list in an overlay kept inside the viewport.

AGENTS.md ranks the initial bundle above every other frontend rule. Building the Sources row
menu (slice 17.6) measured these costs to the initial total, against the 253.47 kB base:

- The CDK focus key manager (`@angular/cdk/a11y`, which `CdkMenu` uses), with its type-ahead:
  1.7 kB. Its rxjs schedulers and operators (`debounceTime`, `auditTime`) are reached through
  barrels that `main` already imports, so they land in `main` even from lazy code.
- A first `[class.name]` binding anywhere in the app: 4.3 kB, Angular's styling algorithm, which
  lives in core and so in `main`. The library binds modifiers with `[attr.class]` instead.

With neither, the menu costs 0.14 kB of the initial total (253.61 kB).

## Decision

- `kx-menu` composes the library's icon button (`aria-haspopup="menu"`, `aria-expanded`) and
  opens its list, `kx-menu-panel`, in a CDK overlay. It uses the functional overlay API
  (`createOverlayRef`, `createFlexibleConnectedPositionStrategy`,
  `createRepositionScrollStrategy`), as the CDK dialog does.
- Focus moves through a small pure helper, `menuKeyTarget`: the arrow keys step and wrap, Home and
  End go to the ends. There is no type-ahead, since the menus have four items or fewer.
- Focus leaving the list closes it, whether by Tab or a press elsewhere, so only one menu is open
  at a time without a registry.

## Consequences

- The menu follows the WAI-ARIA pattern that L2-063 asks for, proven by
  `e2e/admin/sources-menu.spec.ts`, without the CDK Menu's submenus, type-ahead or pointer-aim
  handling. Keryx's menus have no submenus.
- If a menu needs submenus or type-ahead, or the CDK key manager is already in the initial bundle
  for another reason, the CDK Menu is the better choice. This ADR is then superseded, and the
  bundle measured again.
- A component binds a BEM modifier with `[attr.class]` and a computed class string, never
  `[class.name]`, until a slice pays for the styling algorithm with a stated reason.
