# 0012. "Edit source" opens Sources until sources can be edited

## Status

Superseded, 28 November 2026, by journey 17 (staff maintain sources), as its Consequences
foresaw. "Edit source" now opens the edit source dialog for the one source still failing, from
wherever the notification is. "Edit sources" still opens Sources, which lists failing sources
first; L2-062 has no health filter to narrow it further. Accepted, 28 November 2026 (journey 16,
staff retry failed sources).

## Context

L2-059 AC2 and AC3 give a retry's outcome notification the action "Edit source", or "Edit
sources" when more than one still fails. The Retry failed sources design leaves its target
`<TO SUPPLY>`: the edit source dialog edits one source, and it does not exist yet. Journey 17
builds it, with the Sources screen's health and edit and remove (L2-003, L2-060). The action
cannot wait for that journey without leaving AC2 and AC3 incomplete, and it cannot open a dialog
that is not there.

## Decision

- Both actions open the Sources screen (`/sources`), the one place a source can be looked at
  today, after closing any open CDK dialog by its ref. The notification names the sources that
  still fail, so staff know which to look for.
- The label follows the mocks: "Edit source" when one source still fails, "Edit sources" when
  more do. A retry in which every source recovered offers no action.

## Consequences

- When journey 17 builds the edit source dialog, "Edit source" opens it for the one source still
  failing, from wherever the notification is, and "Edit sources" opens Sources filtered to the
  failing sources. Journey 17 records that in its design and supersedes this decision then.
- Until then, the action lands on Sources' first page, which may not show the failing source
  without a search.
