# 0011. Run chart bars are links narrower than the 44 px touch target

## Status

Accepted, 28 November 2026 (journey 14, staff reviews run history).

## Context

L2-044 AC5 makes each of Overview's 14 bars open its run, by pointer or keyboard, and AC6 keeps
every bar inside the card at 360 px. AGENTS.md asks for touch targets of at least 44 × 44 px. At
360 px the card leaves each bar a column about 20 px wide, so a bar's link cannot be 44 px wide
without overflowing the card or dropping runs. WCAG 2.2 AA (2.5.8, Target Size (Minimum)) allows
a smaller target when the same function is reached through another control on the page that
meets the size; AC6 already names that control: "every run stays reachable … through 'All
runs'".

## Decision

- Each bar holds an `a.run-chart__link` that covers its whole column: the chart's full height
  and half the gap on each side, so a 4 px failed stub is as easy to hit as a tall bar, and the
  links tile the chart with no gap between them.
- "All runs", beside the chart's caption, keeps the 44 px target and opens Runs, where every run
  is a full-width row. A keyboard reaches each bar in turn.
- The caption row is the `section-head` component in a `section-head--caption` variant (a caption
  in place of a heading, the `section-head__link--sm` link, no bottom margin), where the mock lays
  out a `cluster` by hand, so the link is the design system's one component and not a copy.
- The design system's run chart gains the `run-chart__link` element (`components.css`) and the
  Overview mocks draw it.

## Consequences

- The bars are a pointer convenience, below the repo's 44 px rule but within WCAG 2.2 AA through
  its equivalent-control exception. Any other chart that links its marks follows the same rule:
  a full-size control reaching the same places sits beside it.
