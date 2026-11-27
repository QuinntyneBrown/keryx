# Lessons learned

One entry per journey of [journeys.md](journeys.md), added before it merges. Read this file
before starting a journey and apply what fits.

Each entry records what slowed the work, the new lessons it taught and how earlier lessons
worked when applied. Newest entries go at the bottom.

## Journey 1: Scaffolding

**What slowed it**

- **A stale premise.** The work assumed the design system was on `main`; it was on another
  branch, and had to be cherry-picked before a component could be built from it. Check that
  each input a journey names is on the base branch before starting.
- **Running a mutated build.** After a mutation proof the sources were restored but not
  rebuilt, so a manual check ran the broken binary and seemed to show a leak of the query
  string into a span. Rebuild after restoring, before running anything from `bin`.
- **A metric shared by every host in the test process.** The OpenTelemetry SDK listens to a
  meter by its name, process-wide. In a test process one host's provider therefore aggregates
  every host's requests, and when any host is disposed the metric they shared is ended: the
  next host to start gets a second metric of the same name. A test that read "the"
  `http.server.request.duration` metric read the wrong one now and then: it passed every
  local run and failed on CI's first. Read every exported metric of that name, ask for a
  route no other test uses, and flush a provider once, from a host of the test's own. Spans
  cross hosts the same way: match a span by a trace id the test chose.
- **A race fixed on a guess.** The same test had failed once before, and the first fix assumed
  the span was exported before the duration was recorded. A short probe later showed the
  opposite (the measurement comes first, on the request's own thread) and the real cause
  above. Probe the order of events before fixing a race; a fix that only makes the failure
  rarer is not a fix.
- **A failure log nobody can prevent.** On a database that was never migrated, the Npgsql
  provider looks for the history table by selecting from it, so the first `migrate` logs one
  failed command. No public EF Core API avoids it; `tools/deploy/configuration.md` says to
  expect the line.
- **Long shell commands were refused.** A heredoc editing several files, or `git` joined to
  other commands, could not be checked against the worktree and did not run. Edit files with
  the editor and keep each `git` command plain and alone.

**New lessons**

- **Validate settings before the host starts.** `ValidateOnStart` alone makes the host log its
  own "Hosting failed to start" with a stack trace beside the clear line, and two failing
  options classes arrive as one `AggregateException`. `HostRunner` validates first and handles
  both shapes.
- **Write the message yourself.** A data-annotations message is wrapped in the framework's own
  sentence; `Validate(delegate, message)` gives exactly "Missing required setting: `<key>`".
- **Proving "not yet" with a fake clock.** With `FakeTimeProvider` and a `PeriodicTimer`, show
  that nothing happened by stopping the host, which finishes any check wrongly started, and
  then counting; never by waiting.
- **One in-memory store per fixture needs one root.** Hosts that must see the same data (the
  API, the CLI's entry, a test's reader) share an `InMemoryDatabaseRoot`; a store name alone
  is not shared across service providers. Replacing the provider means removing both
  `DbContextOptions<T>` and `IDbContextOptionsConfiguration<T>`, or Npgsql stays configured
  beside InMemory.
- **A span allow-list beats a block-list.** The instrumentation adds tags by version;
  `TelemetryRedactionProcessor` keeps four and drops the rest, so a new tag is private until
  someone adds it. Filter `Microsoft.AspNetCore` logs to `Warning`: their information lines
  print full URLs.
- **Prove a lazy chunk by breaking it.** The spec that the shell loads outside the initial
  bundle passed at once; importing the shell statically made it fail, which is the proof. The
  build's budget was proven the same way, by lowering it below the real total.
- **An empty slot leaves its wrapper.** A component that wraps projected content hides the
  wrapper with `:empty`, or the gap and the padding stay.
- **A health call that retries hides the answer.** `curl --retry` treats `503` as a reason to
  retry; call `/health/ready` once and read the status.
- **Run CI's steps locally first, and expect CI to differ.** Each job's steps were run as CI
  runs them before the workflow was pushed: Release for the backend, and the built app for the
  frontend (`KX_SERVE_BUILT=1`). The workflow itself was right first time; the runner's
  different timing still found the shared-metric failure above. A test that depends on what
  other test classes do at the same moment passes locally by luck.

**How earlier lessons worked**

This is the first entry; the rules in `AGENTS.md` under "Development" stood in for earlier
lessons and held. Judging every command by its exit status caught a format failure a pipe
would have hidden. "Honest red" caught tests that passed at once and proved nothing until the
rule was broken. Reading the initial total after each new Angular API kept `main` inside its
budget (223.86 kB against 250 kB). Keeping background services out of the shared test host,
and running the worker's host on its own, made every schedule test deterministic.

## Journey 2: Staff signs in

- The first endpoint-protection test returned 200 before the fallback policy and 401 after it.
  Check endpoint selection before authorization so an unknown route keeps its documented 404.
  Explicit anonymous metadata on test-only error and telemetry probes keeps their existing
  behavior observable with the production fallback enabled.
- Checking only a successful controller action misses refusals and missing routes. Register
  the admin cache header before the error and authentication middleware, then assert the header
  on each response through the API.
- Run the secure-cookie anti-forgery validator on admin mutations. Its secure transport
  check also runs on safe methods when invoked directly. Test token issuance and submission with one HTTPS client so the cookie takes
  the same path it does in the browser.
- A fixed OpenID Connect discovery document in the API fixture lets the production challenge
  handler generate the redirect, nonce, state and PKCE values without contacting an identity
  provider. Keep a separate startup check for missing deployment settings.
- Verify the current-person endpoint separately for an anonymous browser, an authenticated
  non-staff identity, and a staff identity. Assert the exact JSON field names so a provider
  subject or additional claim cannot become part of the response by accident.
- Test the full code exchange with an in-process provider that signs its ID token and echoes
  the challenge nonce. For other API acceptance tests issue sessions through the cookie
  handler, so their cookies use the same ticket store. Advance the fixture clock to the exact
  expiry threshold; changing either comparison to strict greater-than fails its boundary case.
- Checking the sign-out cookie alone does not verify revocation. Retain the original cookie
  in the test client and send it again after sign-out to verify the server refuses it.
- A button's first class-map binding added over 5 kB of Angular styling code to the initial
  bundle. Using the existing components' attribute binding brought the initial total below
  the scaffold. The production CSS optimizer can also serialize 150ms as .15s. Compare the
  measured duration in milliseconds so a change in its value still fails the behavior test.
- The sign-in mock already supplies separate copy and actions for provider errors, session
  expiry and sign-out. Test each URL state directly. Keep the expiry message accurate for
  both idle and absolute limits, since the API returns the same code for both.
- The forbidden-screen mock shows a switch-account action. The design specifies the complete
  sequence: obtain the anti-forgery token, end the old session, and request the provider account
  chooser. Verify the outgoing provider query as well as the browser request.

**Journey 2, closing slices (shell person, Not found, Settings › Account, expiry)**

- **What slowed it.** Moving the placeholder into a child route grew the initial bundle by 3.2 kB,
  and three bisections (the button template, the outlet, the route shape) each cleared their
  suspect. `NG_BUILD_OPTIMIZE_CHUNKS=0` showed the cause: the build's chunk optimiser folds small
  chunks shared by several lazy screens into `main`. Try that switch before bisecting. Diff
  `--stats-json` inputs with paths normalised from `node_modules/` onward; a second checkout's
  absolute paths otherwise hide every match.
- **A projected icon is styled through its wrapper.** `::slotted` does nothing under emulated
  encapsulation, and a selector into `kx-icon` styles another block. Set `color` on a wrapper
  element the component owns; the icon strokes in `currentColor`.
- **A button that navigates is a link.** The mocks write `<a class="btn" role="button">`; the
  library renders `<a class="btn">` (ADR 0006). Locators by BEM class are unaffected.
- **A missing state is a mock first.** Sign-out failure had no mock; adding
  `settings/sign-out-failed.html` (and its entry in the manifest, the index and every state bar)
  took minutes and gave the spec its copy.
- **Pin a port per worktree.** `reuseExistingServer` would run a spec against another
  checkout's server on the default port; `KX_ADMIN_PORT` set per worktree avoids it.

**How earlier lessons worked.** "Prove a lazy chunk by breaking it" and "honest red" both held:
the expiry spec passed at once and was proved by breaking the guard's `session_expired` mapping.
"Read the initial total after each new API" caught the 3.2 kB growth in the same slice it
happened.

## Journey 3: Staff adds a source

**What slowed it.** The journey was four designs deep (add, test fetch, fetch respectfully,
the list it lands in) and needed a scorer, a fetcher, a regions endpoint and the first CDK
dialog before the screen could exist: 14 slices, against a 4-day estimate. The initial bundle
went over budget twice, and the CDK Dialog alone pushed it past 250 kB (ADR 0009).

- **A mutation that does not compile proves nothing.** The test run picks up the last binary
  that built, so the "failing" rule is still in place. Check the build's exit status before
  reading a mutated run's results.
- **Two guards can hide each other.** Removing the per-hop safety check on redirects passed
  because the robots.txt lookup refused the same host first. Prove a doubled guard by removing
  both, then each, and say in the test which one it proves.
- **Each new Angular feature has an initial-bundle cost, even in lazy code.** A signal
  `contentChildren` query cost 6.6 kB and the first `@for` about 5 kB. A `cells` input of
  `TemplateRef`s did the same job for nothing. The CDK Dialog costs about 15 kB of core and
  RxJS that cannot be split. Bisect with `--stats-json`; a build over budget writes no stats,
  so raise the budget for the measurement only.
- **An input named `id` duplicates the host attribute.** `#source-url` then matched the host and
  the control. Name it `controlId`.
- **The CDK leaves `aria-modal` false.** Pass `ariaModal: true`, and label and describe the
  container by the dialog's own ids.
- **Enter submits only with a submit button.** Every `kx-btn` was `type="button"`; a `submit`
  attribute on `btn` lets the dialog's form handle Enter and the button alike.
- **A new call in a shared flow breaks every spec that walks it.** The test on blur made six
  existing specs send an unanswered request. Answer it in the spec's shared helper, since later
  registrations take precedence, and add it explicitly where a spec builds its own handlers.
- **Prettier rewraps imports.** An edit script matching the old one-line import failed its
  assertion and wrote nothing, as it should. Take old text from the file as it is now.

**How earlier lessons worked.** "Honest red" caught two weak tests: the user-name rule passed
its mutation until a row without a password joined the table, and the close lock was proved by
removing `disableClose`. "Read the initial total after each new API" kept every frontend slice
at the same 253.40 kB after the dialog. The deferred pieces are recorded where they will land:
the next run's time in the toast (journey 4), "Edit source" on the warning toast (journey 17),
the audit entry on create (journey 24), and the list's held and 30-day counts (journeys 5–6).

## Journey 4: Worker collects on schedule

**What slowed it.** Three designs (schedule, pipeline, run record) met in one worker, with many
`<TO SUPPLY>` values to settle: the re-check bound, the pickup interval, the skipped start's
severity, the issue prefix, the held-only outcome and the reading of the clock-change days. The
pipeline's later stages need tables from journey 5, so ADR 0010 records what arrives when.

- **Capture `$?` before anything else in the same line.** A mutation loop printed
  `"$(basename $f) … test=$?"`: the command substitution ran first, so every mutation "survived".
  Assign `t=$?` straight after the command, then print. When every mutation survives, suspect
  the loop before the tests.
- **A timer's due time is not its identity.** `SignallingTimeProvider.TimerAsync(30 s)` matched
  the readiness gate's 30-second timer instead of the scheduler's, so the test advanced before
  the scheduler had ticked; it failed once in a full run. Choose a due time no other timer of
  the host uses, and list the host's timers (here 10, 30, 60 seconds) in a comment.
- **A guard that no behaviour reaches is dead code.** Skipping the spring-forward hour changed
  nothing: the missing time maps onto the next slot's instant. Its mutation survived, and the
  guard was removed rather than kept "for clarity".
- **A test's setup can make a rule unreachable.** The later-stage reset, the execution guard,
  close-on-success and the sanitizer all survived their first mutation because the test's data
  never needed them (later stages already not run, a lease already free, a closed issue replaced
  anyway, an address with nothing to strip). Each needed one more line of setup.
- **A new hosted service changes every worker test.** The runner made the scheduler tests'
  worker execute runs too, and the worker needed the fetch contact URL, so the startup
  validation test failed until its arguments gave one. Run the whole worker suite after adding
  a hosted service.
- **Analysers name things.** CA1711 refused `RunCollection` (ends in "Collection"); the design's
  own `RunCollectionCommand` was the answer.

**How earlier lessons worked.** "A mutation that does not compile proves nothing" caught two
mutations that failed to build and one with unreachable code. "Two guards can hide each other"
did not arise; "a new call in a shared flow breaks every spec that walks it" did, in its backend
form (a new required setting). Deferred, and recorded where they land: `next_run_at` served on
`/v1/status` and the admin overview (journeys 10 and 13), the toast's next-run time (journey 13),
the store-failure and API-unchanged criteria with the store stage (journey 5), issue listing and
muting (journey 20), and the news query and platform sources (their clients).

## Journey 5: Worker ingests idempotently

**What slowed it.** Three designs (ingest-idempotently, store-records, keep-facts-not-copies)
met in two stages, with `<TO SUPPLY>` values to settle: the item-URL rules, the merge window's
clock, the over-long title and tag, the category rule, the organization kind, the event
module, the DST reading of an event's local time and the noindex count. The headline merge
needs "the same organization", so organizations moved ahead of merging in the slice order.

- **zsh does not split an unquoted variable.** A mutation loop passed `--filter` and its value
  in one variable; zsh handed `dotnet test` a single argument, every run errored, and every
  mutation looked killed. Pass each argument in its own quoted variable, and start every loop
  with a control run that changes nothing and must pass.
- **A test fixed to one machine's zone hides a rule.** `DateTime.TryParse` converts an offset to
  the machine's zone; on a Toronto machine with a Toronto region, dropping the zoned branch
  changed nothing. A case whose region zone differs from both the machine's and the offset
  (`+01:00` read for `Europe/London`) proves it anywhere.
- **Ask what decides order before asserting it.** Sources reach the reader in the order they
  answer, so "which item becomes the article" in a same-run merge varied between runs. Assert
  the order-free facts (one article, both links), and make an order a rule only where the code
  fixes it (organizations are named by the earliest-added source).
- **A fingerprint is a list of what counts as a change.** The event's time was not in the
  article's fingerprint, so a moved event was "unchanged"; then the staged row rebuilt its facts
  without the event, so every event counted as updated. A test for each direction caught one
  each.
- **The context remembers what it saved.** Discarding a failed run's staged rows by query was
  dead code: the same context had staged and saved them, so its tracked rows were enough. The
  surviving mutation said so.
- **Analysers name things, again.** CA1716 refused `Event`; it is `EventRecord`.
- **A session does not outlive a jump of the clock.** Opening the staff client before advancing
  the fake clock six hours gave a 401; open it after.

**How earlier lessons worked.** "A guard that no behaviour reaches is dead code" removed the
spring-gap branch in `EventTime` at once. "A test's setup can make a rule unreachable" found the
link-only merge test that never reached the live-article organization filter, and the
fail-first store fault that never saved a row. "Capture `$?` first" held. Deferred, and recorded
where they land: the robots meta tag in HTML pages (the page parser, with website sources), the
null organization of a news-query item (the news client), `GET /v1/regions` and focus-area
regions (journeys 10 and 18), and serving events and organizations (journey 9).

## Journey 6: Worker scores and publishes

**What slowed it.** Little: the scorer, `RelevanceScore` and `PublicationPolicy` arrived with
journeys 3 and 4, and journey 5 already stored a score and a status. What remained was the
decision an article keeps (reason, threshold, who decided), the rule that a run never overrules
staff, and the frontend's copy of the case table.

- **Pick the float case by running it.** `0.1 + 0.2 + 0.3` drifts above 0.6 and `0.3 - 0.1`
  multiplies back to exactly 20, so neither caught a comparison made without rounding to
  hundredths; `0.57 * 100` (56.99999999999999) did. Print the value in `node` before writing
  the row.
- **Work expected scores from the weights in the test.** Writing "one topic in Toronto:
  0.6 × 1/3 + 0.4 = 0.60" above the items made the threshold cases (0.60 published at T 0.60,
  held at T 0.65) exact, with no guessing at the scorer.
- **An exact-keys assertion catches every new field.** The article's three new fields broke the
  keep-facts test's key list at once, which is its purpose: the list is updated deliberately.

**How earlier lessons worked.** "Start every mutation loop with a control run" ran first in
each loop and passed. "A session does not outlive a jump of the clock" was applied up front:
each test opens its staff client after its last advance. Deferred: the threshold help text and
the article detail that use `publicationStatusFor` (journeys 11 and 18), the staff decision
itself (journey 12), and an event with no linked article (none is made yet: every event comes
from an article).

## Journey 7: Staff issues an API key

**What slowed it.** The initial bundle. The API screen's first `[class.x]` binding brought
Angular's styling instructions into `main` (+4.66 kB, over the 255 kB budget) although the screen
is lazy; bisecting by putting files back to `HEAD` found it in three builds, and `[attr.class]`
with the whole class string brought `main` back. Five library components were also missing
(`badge`, `tag`, `callout`, `copy-field`, `check` with `check-list`) and were built first.

- **Bind `[attr.class]`, never `[class.x]` or `[style.x]`.** Every library component already did;
  a screen's template is no exception, because the instructions live in `@angular/core`, which
  is in `main`. Read the initial total after any new template binding form.
- **Ask "visible" of the dialog's `.dialog`, not of the CDK container.** The container has no box
  of its own (the panel is fixed-positioned), so `toBeVisible()` on `getByRole('dialog')` waits
  for ever; assert the role's name and description on the container, and visibility on `.dialog`.
- **A guard the disabled control already gives is dead code.** Removing `if (this.busy()) return`
  before a request left every test green, because the busy button is disabled; it was removed,
  not kept as untested defence.
- **Prove "gone when the visit ends" with a store that leaks.** A mutant that kept the revealed key
  in a module variable failed the leave-and-return test, which a mutant of the `onDestroy` reset
  alone could not show: the screen's store dies with the screen.
- **A rule that needs the next slice survives until then.** "A revoked client's name is free"
  survived the create slice's mutations and was killed by the revoke slice's test; note such
  survivors when they appear rather than forcing a test early.

**How earlier lessons worked.** "A session does not outlive a jump of the clock" struck again (a
one-hour advance gave 401) and was fixed by a five-minute gap that proves the same rule.
"InMemory raises no DbUpdateException" was applied up front: the name race is proven by the
`SaveFault` switch. Deferred: the 401 for a revoked key on `/v1` (journey 8), the audit entries
(journey 24), and the usage numbers in the table and the Revoke dialog (journey 21).


## Journey 8: Product reads articles

**What slowed it.** Test fixtures that did not mean what they said. A region outside the focus
areas (Scarborough) held every article back at a 0.48 score, so filter tests failed for a reason
that had nothing to do with filters; a "later than the run" date that was earlier than the run
put the no-time article in the wrong place. Both were the tests' arithmetic, found by reading the
seeded settings and the fixture clock, not the code. The ETag slice lost a run to `HttpClient`
rewriting `Cache-Control` as `max-age=60, private`.

- **Work a fixture's numbers from the seeded settings.** An item is published only when its topics
  and its region score at or above the threshold: use a focus-area region (Toronto, Mississauga)
  for anything a product must see, and say so in the test's comment.
- **Assert parsed headers, not their text.** `HttpClient` re-serialises `Cache-Control` in its own
  order; compare `CacheControlHeaderValue`'s fields (private, max-age and that nothing else is set).
- **A guard no request can reach today gets a direct test.** The ETag filter's "not 200" guard
  survived every API mutation because errors are thrown, not returned; `[ApiController]`'s
  automatic 400 is a returned `ObjectResult`, so a test of the filter with such a result proves it.
- **Fold a slice into the next when they share every file.** Filters were planned as their own
  slice but changed the same query, validator and cursor fingerprint as paging; they were built
  together with each rule still mutation-proven, and the journey kept six planned slices' worth
  of tests.
- **Keep the per-instance limits honest in the settings doc.** In-memory rate windows let n
  instances allow n times the limit; `tools/deploy/configuration.md` says so beside the setting.
- **Name the open decision when you close it.** Each `<TO SUPPLY>` this journey met (cursor expiry
  and fingerprint, the no-time sort, `org`'s code, store sharing, forwarded headers, the key names,
  the document's anonymity) was replaced in the design in the same commit as the code.

**How earlier lessons worked.** "Start a mutation run with a control" caught that a rewrite was an
equivalent refactor, not a mutant (`ObjectResult` pattern). "Inject DbUpdateException" was not
needed: nothing here writes. Deferred: AC3 of L2-032 names staff hiding an article, which arrives
with journey 12, so the changed-content case is proven here by a newly published article; the
Data Protection key ring is not yet persisted, so cursors (and sessions) do not survive a restart
or cross instances, and that store stays `<TO SUPPLY>` in authenticate-api-clients.
