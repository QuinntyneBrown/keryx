# 0007. Public ids are a prefix and 12 random base32 characters

## Status

Accepted, 27 November 2026 (journey 3, staff adds a source).

## Context

L2-009 AC4 requires that a source, article or run id be prefixed `src_`, `art_` or `run_`, never
reused, and unchanged for the life of the record. The designs leave the generation scheme
`<TO SUPPLY>`, and the mocks show sequential ids such as `run_2418`. The apply-retention design
adds that an id must never come from a value a delete frees.

A sequential id taken from the database key reveals how many records exist, lets a caller guess
its neighbours, and depends on the sequence never being reset.

## Decision

`Keryx.Domain.PublicIds.New(prefix)` draws 12 characters from the lowercase base32 alphabet
(`a`–`z`, `2`–`7`) with `RandomNumberGenerator`, giving `src_` plus 12 characters (60 bits). Each
entity stores its public id in its own column with a unique index. An id is drawn once, when the
record is created, and never changes. The database key stays internal and is never served.

Rows that existed before the column was added draw their ids in the migration that adds it.

## Consequences

- Ids look like `src_k3v9q2mfa7xd`, not like the mocks' `run_2418`. The mocks are illustrative
  and are not changed.
- A collision is about one in 10^18 for a million records. The unique index turns one into a
  failed insert rather than two records sharing an id.
- An id is not a cursor: lists are paged by a sort key, never by id order.
