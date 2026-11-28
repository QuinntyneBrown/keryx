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

## Journey 9: Product reads events and organizations

**What slowed it.** A 500 with no log: the organization filters called a helper
(`ServedRecordFilters.ServedArticles(articles)`) inside a LINQ lambda, which EF cannot inline,
and the test host's logs are quiet. A throwaway test that sent the query through `ISender` and
printed the exception found it in one run. The cursor also had to change shape: a list sorted
by name needs a text sort value, which J8's time-only position could not hold.

- **Build an EF subquery outside the lambda and capture it.** `var served = ServedArticles(articles);`
  then `served.Any(...)` inside the `Where`/`Select`; a method call inside the expression is not
  translated.
- **When the API answers 500 in a test, send the request through `ISender` in a scratch test.**
  The exception comes back whole, with the expression EF could not translate; delete the probe.
- **Give a position a text sort value and serialise it as JSON.** A name may hold any character,
  so the cursor holds `{list, filters, sort, id}` as JSON inside Data Protection; time-sorted
  lists read `Time` from ticks. Changing the protector's purpose (`v2`) retires old cursors
  cleanly.
- **Seek with the comparison the database sorts with.** `Name.CompareTo(sort) > 0` follows the
  same collation as `OrderBy(Name)` in PostgreSQL and the same comparer in InMemory, so the seek
  and the sort agree; an ordinal comparison would not.
- **Name a related record only when it reads on its own endpoint.** A source names its
  organization only once that organization is listed, so no product meets an id that 404s.
- **A guard only Keryx's own key can reach gets a codec test.** Payloads protected with Keryx's
  key that are not positions (`null`, an array, missing fields), and ticks beyond
  `DateTimeOffset.MaxValue`, are proven refused directly, since no product can forge them.
- **A test that seeds too little issues no cursor.** Paging needs more listed rows than the limit;
  count what the seed lists, not what it adds.

**How earlier lessons worked.** "Work a fixture's numbers from the seeded settings" was applied up
front (focus-area regions, a topic word in every title meant to publish) and spared the
J8-style misses. "Assert every match before writing" saved a half-applied design edit: the script
refused to write, though the commands after it in the same line still ran; keep edits and
staging in separate commands. Deferred: source removal (journey 17) adds the active filter to
`/v1/sources`; an event whose article staff hide (journey 12) is proven gone then.

## Journey 10: Product reads reference data and status

**What slowed it.** Two test assumptions. The seeded news query is never fetched by a run (its
platform has no client yet), so "the news query fails" gave a run that succeeded; an unanswered
RSS source made the issue. And a leak check for the word "issue" matched the legitimate status
`completed_with_issues`.

- **Make a failure with a source the run actually fetches.** An RSS source nothing answers fails
  with `not_found`; a seeded news query is skipped and proves nothing.
- **A leak check's words must not be legal output.** Probe for what must never appear (a source's
  name, its address, a failure code), not for words the contract itself uses.
- **A mutant that does not compile proves nothing, and its test runs a stale binary.** Read the
  build status beside each mutant; redo it in a form that compiles (here, shifting the clock
  instead of replacing it with a literal).
- **Insert runs directly when the outcome is the input.** `Run.Start` then `Fail` through the
  `KeryxDbContext` gives a failed, running or other-worker run without staging a pipeline fault.

**How earlier lessons worked.** "Build an EF subquery outside the lambda" was applied first time:
the served-article counts inside the category and region projections are captured variables.
"Name the open decision when you close it" closed all five of this design's `<TO SUPPLY>`s.

## Journey 11: Staff reviews articles

**What slowed it.** Three tests that passed with the rule broken. Fast key presses through the
status tabs dropped a choice, but real key presses were too slow to show it until the keys were
dispatched in one task, and the bug was then in the Tabs component's dedupe against an input
that had not updated yet. A missing 300 ms debounce went unnoticed because the router cancels
superseded navigations, so only the last term was ever fetched. And the panel opened in a
CDK dialog that Playwright called hidden, because its container was 0 × 0 around a
`position: fixed` panel. Two Angular costs also surfaced: the first `[style.--x]` binding and
the first `@defer` each grew the initial bundle by about 6 kB from a lazy screen.

- **Dedupe where the state updates at once, never against an input.** A signal input changes
  only on the next change detection, so a component that ignores "the value I already have"
  drops a second change made in the same task. Emit every choice and let the owner of the state
  (here the page's query, set before navigating) decide.
- **Prove a same-task race by dispatching in one task.** `page.evaluate` sending each `keydown`
  to `document.activeElement` in a loop is deterministic; `keyboard.press` in a loop is not.
- **A debounce or a delay is proven by a fake clock in a unit test**, not by an e2e count: the
  router and `AbortController` hide extra requests. Put the timer in a small domain helper
  (`searchPause`, `skeletonDelay`) and advance `vi.useFakeTimers()` to 299 and 300 ms. In e2e,
  `page.clock.install()` then `runFor(1000)` fast-forwards past a pause when an absence after
  it must be shown.
- **No style bindings, no `@defer` until one pays for itself.** `[style.--value]` pulled 5.2 kB of
  styling instructions into `main`, and `@defer` 6.6 kB of its runtime, both from a lazy screen.
  An SVG with a bound `width` attribute draws a meter with neither inline styles nor style
  bindings (the lint rule forbids `[attr.style]`). Measure with the stats JSON of a base build in
  a scratch worktree.
- **Give a CDK dialog's pane a size when its content is fixed.** `width: '100%'` and
  `height: '100%'` make the dialog's box the panel's, so it is visible to tests and to
  assistive technology's geometry.
- **A line whose removal no test notices is either missing a test or dead.** Closing the dialog
  before navigating was redundant once the URL change closed it, so it went; a source fallback
  needed a renamed-source case to matter.
- **Extract a shared fetch helper at the third copy.** `readJson` replaced three private copies.

**How earlier lessons worked.** "Read the initial total after each new Angular API" caught both
6 kB growths before commit, and diffing `--stats-json` inputs against a base build named the
chunk at once. "Honest red" found all three weak tests above; each survived mutation and got a
test that fails for the right reason. "Close a CDK dialog by its ref" held: the page keeps the
ref and closes it when the viewport widens. Deferred: Publish and Hide in the panel (journey
12), "Run now" and the next run's time in the empty state (journey 15), "Export CSV" (journey
22).

## Journey 12: Staff publishes or hides an article

**What slowed it.** Little: the backend took three slices without a surprise, because the
article already kept staff decisions through later runs (journey 6). Two plan changes came from
reading the acceptance criteria against the slices. The Publish notification must carry a
working Undo, so a separate Undo slice would have shipped a dead button first; the two became
one slice. And "within the same session" needed the server to know the session, which the
cookie's ticket did not carry.

- **Fold a slice into the one whose output needs it.** When a criterion of slice A shows a control
  whose behaviour is slice B, build them together rather than ship a control that does nothing.
- **Put a server-side session's id in its ticket.** The ticket store adds the session's id as a
  claim before serializing, so every later request carries it, and a rule can say "only the
  session that did this". A session signed in before the claim existed has none, so the rule
  must refuse an empty id: two such sessions would otherwise match each other.
- **Make a design-time factory the startup project for migrations.** `dotnet ef migrations add`
  with `-s src/Keryx.Infrastructure` uses `DesignTimeKeryxDbContextFactory`; the API project has
  no EF design package.
- **Signal inputs still call `ngOnChanges`.** A library component can react to an input's
  transition (busy → idle) there, with a cleared `setTimeout` for after the render, and add no
  `effect` or `afterNextRender` to the bundle.
- **A dialog's component needs every input pushed.** A component opened by the CDK dialog takes
  no template bindings, so the page sets each input (`shown`, `busy`) through `setInput` when
  they change, and a test at the narrow width proves it.
- **`:host:has(> .x)` lets a child component share a flex row without a host class binding**,
  whose styling instructions would join the initial bundle.
- **A field the screen never shows proves nothing when it updates.** Clearing `status_reason`
  after a decision survived mutation (the alert shows only for statuses the rule set), so it
  went; Undo reads the article again instead, which the restored alert proves.

**How earlier lessons worked.** "Dispatch in one task" made the double-press test exact: two
`click()` calls in one `evaluate` reach the store before the button can be disabled, and the
store's guard is what stops the second. "Read the initial total after each new Angular API" held
the bundle at 254.19 kB. "Name the open decision when you close it" closed all seven of this
design's `<TO SUPPLY>`s, with auditing of decisions deferred to journey 24, which builds the
audit log. The journey 9 deferral is closed: an event whose article staff hide is no longer
listed or served.

## Journey 13: Staff checks the Overview

**What slowed it.** The screen's states. The plan had 8 slices; reading the mocks against the
criteria split the last one three ways (empty and page error, section errors, skeletons) and
moved following a run live to journey 15, whose "follow it live" it is, so it landed as 10. The
skeletons took the longest: the mock's bars are shorter than the lines they stand for, so the
lists below jumped 22 px when the status arrived, and a second pass found the Next run stat (a
small value with a sub-line) was the tallest of the row. Ten library components arrived with the
screen (`Stats`, `StatusCard`, `RunChart`, `SectionHead`, `List`, `IssueCard`, `Stack`, `Rail`,
`Progress`, `Spinner`), each from its design-system page.

- **Put a skeleton inside the element it stands for.** A skeleton line in the real `<p>` with a
  no-break space is exactly as tall as the line that replaces it at every breakpoint, where a
  bar of tuned height is right at one. Give a loading component the shapes of what will come
  (a stat's size, whether it has a sub-line) rather than a count. Text that wraps at XS cannot
  be foreseen; say so in the design instead of tuning for one string.
- **A bar's length as a `data-value` attribute with one rule per percent** keeps the design
  system's markup (`<li class="run-chart__bar">`, `<div class="progress__bar">`) without a style
  binding, and costs nothing in the initial bundle. Measured again here, the first `@defer` would
  have cost 8.6 kB and broken the budget, so `L2-088` AC4's defer gave way to AC1's budget, as
  journey 11 found.
- **Show a retry's answers together.** When "Try again" repeats four requests, a section that
  answers first makes the page stop being "all failed" and the error vanish while the rest are
  still pending. Hold each answer as a function and run them once the last is in; the busy button
  stays on screen as L2-073 asks.
- **"All failed" is not "not all ready".** A test that holds one section open while the others
  fail is what tells the page error (every request failed) from a partial failure, and it killed
  the two mutants the empty and page-error slice left alive.
- **A static `href` on a component's host passes an `href` check without the link.** Locate the
  rendered element (`.empty__actions .btn`), not the component tag, or the test proves the
  template, not the page.
- **A count that equals another hides which one a field reads.** Sources checked and sources OK
  were both 38 in the mock; a second case with 40 and 37 is what proved the bar reads the first.
- **A calendar rule counts calendar days in the zone.** "Since Mon" by weekday up to six days
  back is a difference of two `en-CA` day keys, not of 24-hour spans, which give different
  answers for the same day at different hours.

**How earlier lessons worked.** "Read the initial total after each new Angular API" caught
`@defer` (262.75 kB against a 255 kB budget) before it was committed, and the total ended at
253.71 kB. "Name the open decision when you close it" closed the design's issue-title and
navigation-target `<TO SUPPLY>`s, recording what waits for the Runs screen (journey 14) and for
following a run live (journey 15). "A line whose removal no test notices is either missing a
test or dead" removed a redundant `Math.max` seed, a rounding of an exact division, a dead
`min-width` and the progress bar's unreachable clamp.

## Journey 14: Staff reviews run history

Twelve slices: the shell's schedule endpoint, the list, the detail card, the pipeline, the
issues, a failed run, the problems filter, the empty and error states, loading, narrow screens, a
run in progress and Overview's links into Runs. The list and detail endpoints existed from
journey 4, so the backend was one new endpoint and two additive list fields. What slowed it:
Prettier rewrapping templates under edit scripts (four failed asserts, each reread), a
mutation loop whose control run failed on a flaky check, so its results meant nothing until the
check was fixed, and three `<TO SUPPLY>`s (detail error, the narrow way back, the running
publish line) that needed a mock state or a rule before code.

- **Run the control first and read it.** A mutation loop's unchanged run (`s/ZZZ//`) must pass;
  when it failed, every "killed" mutant beside it was noise. A check that raced a 300 ms timer
  under Playwright's clock was removed; the timer's delay is proven by its unit test.
- **A store that clears what a new choice replaces** shows the right thing while the next answer
  is pending: choosing a run clears the previous detail and its error, or the old run's heading
  (or its error) stays on screen under the new run's highlighted row.
- **Settle each read in `finally`.** Starting and stopping a skeleton timer from both the success
  and failure paths left gaps a mutant found; one `settle()` in `finally`, which does nothing
  once the visit has ended, covers both and never outlives the screen.
- **Disable a control only while it cannot work.** Disabling the filter whenever the list
  reloaded trapped a person who switched filters mid-load; the loading mock disables it only
  before the first list, and so does the screen.
- **A rotating element's bounding box grows.** Compare a spinner's `offsetWidth`, not its
  `getBoundingClientRect`, against the icons beside it.
- **A link can be narrower than 44 px only beside a full-size way to the same place.** The run
  chart's bars at 360 px are about 20 px wide; WCAG 2.5.8's equivalent-control exception and
  "All runs" carry it, written down in ADR 0011, rather than the 44 px rule being broken
  silently.
- **A taller control changes the skeleton it replaces.** Adding the 44 px "All runs" link made
  the chart's caption row taller, and the loading spec caught a 26 px shift; the loading card's
  caption line is now as tall as the link's target.
- **`afterNextRender` with an injector** moved focus after a route change (to "Back to runs", and
  back to the run left) without growing the initial bundle (253.71 kB before and after).

**How earlier lessons worked.** "Put a skeleton inside the element it stands for" made the Runs
skeletons line up first time at every width; the no-shift checks passed without tuning. "Avoid
`[class.x]` bindings" kept every new variant (stats, rail, spinner, section head) on attribute
bindings, and the initial total stayed at 253.71 kB through all twelve slices. "A count that
equals another hides which one a field reads" shaped the fixtures: the running run's sources
checked (38) differ from its total (64) and from its added count (21).

## Journey 15: Staff starts a collection now

Ten slices: the start endpoint and its lease race, then "Run now" on Runs and Overview, the run
already in progress, a failed start and double presses, "Collecting…", following a running run,
its end, the outcome notifications and a scheduled run on any screen. What slowed it: the design
split the poll between `RunsStore` and `CollectionWatcher` with a pause handshake, and building
it that way polled twice at the 3-second mark; tests that proved an absence ("no further read")
passed under mutants until each had a barrier; and fixtures that described two worlds at once (a
list still running after its run ended, a status with no run while Runs showed one) failed for
reasons that had nothing to do with the code.

- **One poller by construction beats two that pause each other.** The watcher is the only reader
  of the run in progress; Runs shows the reads it hands out (`onRead`). The design and its
  sequence diagram were changed to match, in the same slice.
- **An absence needs a barrier.** A timer's request is still in flight when `runFor` returns, so
  "no read" passed while reads were being sent. Send a request after it (`fetch` from the page,
  or opening another run) and count once that has answered: requests reach the routes in order.
- **Make each fixture one consistent world.** When a run ends, the detail, the list and the
  status all say so; a helper that serves all three from one phase variable kept the
  follow, end, outcome and schedule specs honest.
- **A test written for an earlier slice can encode a rule the next slice rightly changes.** The
  "already in progress" case opened on a list that showed the run, which, once "Collecting…"
  existed, correctly disabled the button; the case became a run that began after the read.
- **A new test can expose an old gap.** The failed-start notice found that danger notices
  dismissed themselves after 8 s, against L2-075 AC3; `NotifierStore` sets no timer for them now.
- **Delete what a mutant shows is unreachable.** The once-per-run id, the same-id early return
  and the overlap flag each survived because the structure already guaranteed what they
  guarded; they were removed and the reason written into the design, not kept as comments.
- **Prefer callbacks to `effect` for cross-store events.** `onEnd`/`onRead` with a `DestroyRef`
  kept the first `effect` out of the app; the initial total held at 253.71 kB.

**How earlier lessons worked.** "Run the control first" caught nothing this time but cost
nothing. "A store that clears what a new choice replaces" predicted the bug mutant 6 of slice 7
found: a pending timer of the previous run replacing the chosen run's detail. "Rebuild after a
backend mutation loop" saved a false red in slice 2. "Settle each read in `finally`" carried
over to `runNow`, whose busy state is reset in one place.

## Journey 16: Staff retries failed sources

Four slices: the retry endpoint, the worker merging a retry into the same run, the button with its
notices, and the summary when the retry ends. The design left five `<TO SUPPLY>`s (a refusal's
code and copy, the titles for other counts, the all-recovered notice, the "Edit sources"
target); each was decided in the design, and the one stand-in, "Edit source" opening Sources until
journey 17, has ADR 0012. What slowed it: a fixture that advanced the clock six hours expired the
staff session (401s that looked like a missing route), a partially applied edit script (one file
written before another's anchor failed), and fixtures whose "before" numbers equalled the
mutants' (an earlier run that added nothing, items listed errors-first).

- **Work a run's retryable sources out from what is stored.** No list of source ids is kept: a
  run's failed sources are the open or muted fetch issues that occurred in it. One query serves
  the endpoint's count and the worker's fetch, and a source that recovered since drops out by
  itself.
- **Merge into the run from a baseline taken before the pipeline.** The pipeline records stages
  as a run's own; the handler takes `RetryBaseline` first and `MergeRetry` adds the two passes,
  so the pipeline needed no retry mode beyond which sources it reads.
- **Say a retry's end as a retry, once.** The watcher's ordinary end would have announced
  "Collection finished with 1 failing source" beside "1 of 2 sources recovered"; the run is marked
  a retry on the watcher and the shell announces it one way.
- **Don't advance a test clock past the session.** A backend fixture moved the clock six hours
  between runs; the staff session expired and every request answered 401. Two runs at the same
  instant order by id.
- **Assert every file's anchors before writing any.** An edit script that wrote three files and
  then failed on the fourth's anchor left a half change; the next script built a dict of every
  file's text, asserted all, then wrote.
- **Make the fixture's "before" differ from what a mutant leaves.** `Added = retry.Added`
  survived while the earlier run added nothing, and the "only errors name a source" filter
  survived while errors were listed first; a new item in the earlier run and a warning listed
  first killed them.
- **A test passes "by poll" when timers run in real time.** `setFixedTime` leaves timers
  running, so the watcher's 3-second read showed the retried run as Running without the reload
  the code made; `clock.install` stopped the timers and the mutant failed.

**How earlier lessons worked.** "Make each fixture one consistent world" from journey 15 caught
the first red of slice 16.3 at once: a retried run whose detail still read completed raised
"Collection finished". "An absence needs a barrier" was not needed: every check here was a
presence. "Delete what a mutant shows is unreachable" removed `Distinct()` and a redundant
`track`; two survivors were kept on purpose (a persisted flag cleared, a map entry forgotten),
and the designs say why.

## Journey 17: Staff maintains sources

Nine slices:
- the edit, remove and restore endpoints;
- the "Articles · 30d" and "Last fetched" columns with paging;
- the kind chips;
- the skeleton;
- the row menu;
- the edit dialog;
- the remove dialog with Undo;
- "Edit source" from Runs and from notifications, which superseded ADR 0012.

What slowed it:
- The row menu first added 7 kB to the initial bundle. Finding why took a diff of `main`'s inputs
  against a base build and a bisection by stubbing references. The cost was a first `[class.x]`
  binding (4.3 kB, Angular's styling algorithm) and the CDK focus key manager (1.7 kB), not the
  overlay (ADR 0013).
- The edit mock offered every kind as choosable, against L2-003.
- A zsh variable holding several spec paths ran no tests in one mutation loop, so every mutant
  read as killed.

Lessons:
- **Bisect bundle growth by stubbing, not by guessing.** First diff `--stats-json` inputs of `main`
  against a base worktree built the same way. Then replace a component's body with references
  (`console.log(MenuPanel)`) until the growth goes. Two guesses (a view query, `afterNextRender`)
  changed nothing; the stubs found the cause in four builds.
- **Bind BEM modifiers with `[attr.class]`.** Angular's styling algorithm lives in core, so a
  first `[class.name]` anywhere puts it in `main`. The library already used computed
  `[attr.class]` strings; keep to them.
- **Close an overlay before disposing it.** Removing the panel blurs its focused item, and the
  `focusout` handler then calls `close()` again mid-dispose ("node to be removed is no longer a
  child"). Clear the ref, move focus to the button, then dispose.
- **Let mutants prune the event wiring.** A module-level "one menu open" registry and an
  outside-press subscription both survived: `focusout` already closes the menu on Tab and on any
  press elsewhere. A third survivor showed a missing test, not dead code: no test pressed the
  menu's own button to close it.
- **Keep the rows to keep the focus.** Reloading a list through its loading state replaces the
  table, and focus on a row's button is lost. `refresh()` reads the page in place, and rows
  tracked by id keep their elements. A removed row's focus goes to the table's caption.
- **Correct a mock that contradicts a requirement.** The edit mock's four Type cards contradicted
  L2-003 and the design; mocks are exempt, so they now show the source's one kind, and the design
  says why. The mock's source-specific copy ("may have moved its blog") became the general rule
  the helper words.
- **Use an array for a list of paths in a zsh loop.** `$SPECS` is one argument in zsh, so
  Playwright found no tests and exited 1, which the loop read as "killed". Check that a killed
  mutant names how many tests failed.

**How earlier lessons worked.**
- "Read the initial total after each new Angular or CDK API" caught the menu's growth on the
  first build, before any spec ran.
- "Assert every file's anchors before writing any" meant four aborted edit scripts (anchors
  Prettier had rewrapped) left nothing half-written.
- "Make the fixture's before differ from what a mutant leaves" shaped the Undo tests: the list
  answers with or without the source by a flag the handlers set, so a missing reload shows.

## Journey 18: Staff changes settings

Seven slices:
- `GET` and `PUT /admin/v1/settings` with a version, and a region for each new focus area;
- the form showing every saved value, with skeletons and the error state;
- the save bar and Discard;
- the error summary;
- the save, with its confirmation, failure and conflict states;
- the chip lists and their Add dialog;
- the leave confirmation and the browser's prompt.

The library gained switch, range, chip, input-affix and save-bar, the button variant of
segmented, and the form variant of page-body. The initial total stayed at 253.61 kB.

What slowed it:
- Four states had no mock: a failed save, a refused save, the Exclude and Focus-areas dialogs,
  and the leave confirmation. Each was added as a mock before it was built.
- A test namespace named `Settings` hid the `Settings` entity in every test file of the
  assembly, which broke an unrelated fake's build.
- A `*/` in a doc comment ("cron: 0 */3 * * *") ended the comment.
- `scheduleLine` already existed, for Runs, so the domain's barrel refused a second one.

Lessons:
- **Name a test folder after its feature, not after an entity.** `Keryx.Api.Tests.Settings`
  shadowed `Keryx.Domain.Settings` for the whole test assembly; `AdminSettings` did not.
- **A submit button outside its form names it with `form`.** Enter in a field submits only a form
  that has a submit button. The save bar's "Save changes" sits outside the form, so `kx-btn`
  gained a `form` input and `kx-page-body` a form id. One handler, `submitted`, serves the button
  and Enter.
- **The CDK hides the page while a modal is open.** A role locator (`getByRole('list', …)`) finds
  nothing behind a dialog. Assert the page after it closes, or prove the absence through
  something that follows.
- **`toHaveText` normalizes whitespace, so it cannot prove a trim.** Prove it by what is sent:
  a mutant that skipped trimming survived until the test read the `PUT` body.
- **Let mutants decide what a state reset is for.** Clearing "new" chips on Discard survived
  because nothing can show it, and it was removed. Clearing them on load died to a real case:
  reloading after a conflict, when the server's list holds the value just added. The same held
  for the `beforeunload` listener: releasing it after a load, a save and on destroy each needed
  its own test.
- **Hold a `beforeunload` listener only while something is unsaved.** The store adds and removes
  it as the entries change, and needs no `effect`, so `main` did not grow. Playwright proves it
  with `page.close({ runBeforeUnload: true })` and the `dialog` event.
- **Write a test's counts as their parts.** `5 + 5 + (8 + 1) + (3 + 1) + (9 + 1)` caught my
  miscount at once, where one total would have been corrected to whatever the page showed.
- **Two timers need a test that overlaps them.** Clearing the confirmation's timer at each new
  state survived until a second save within 8 seconds was tested.

**How earlier lessons worked.**
- "Diff the initial total after a first use of an Angular API" was cheap here: `afterNextRender`
  and the CDK dialog were already paid for, and `effect` was avoided. Every slice read 253.61 kB.
- "Make bad data bad by construction" shaped the validation tables: one row for each bound, on
  both sides, in the server's and the client's suites.
- "Take old text from the file as it is now" was needed again. Prettier rewrapped the Settings
  template between slices, and a stale anchor stopped one edit script before it wrote anything.
  An earlier loop, though, wrote files before its assert failed, and a second script had to
  finish it. Assert every file before writing any.

## Journey 19: Staff receives alerts

Five backend slices:
- the source-failing alert, once per streak;
- the failed-run alert;
- a refused send as an `alert_not_sent` warning, retried once on the next run;
- the 08:00 digest worker;
- the MailKit sender with its startup settings.

The admin app gained only the new issue kind's title. The design's four `<TO SUPPLY>`s were
settled in the design before the first test: the mail-failure kind (added to L2-020), the
digest's counts and outcome, the e-mail wording, and the timeout.

What slowed it:
- A mutant that made the digest schedule fire at exactly 08:00 put the worker in a tight loop.
  The worker test hung and had to be stopped by hand.
- `AddMail` first chose the sender from configuration at registration, before the test host's
  settings were added. The worker built with the wrong sender.
- A validator that read another options type validated that type a second time, so the startup
  error named one setting twice.
- MailKit 5 sends a `SendRequest`, not a `MimeMessage`.

Lessons:
- **Run a schedule's mutants against its case table, not the worker.** A rule that decides when a
  loop wakes can, mutated, make it never wait. Keep the rule pure (`DigestSchedule.NextAfter`),
  table it, and mutate it under the table alone.
- **Choose a service from options when it is resolved, not from configuration when it is
  registered.** A factory over `IOptions` sees every source the host adds, test sources included.
- **Put a cross-options check on the options that depend.** `Validate<IOptions<T>>` on one type
  reads `T`'s value, and that runs `T`'s validators again. The origin is checked on the mail
  settings, and the alert options carry no validator.
- **Compose before you send.** A message composed inside the send's `try` turned a configuration
  fault into a "mail refused" warning. Only the transport's call belongs in the `try`.
- **An idle staff session expires when the test clock moves hours.** Open a new client after
  advancing past the idle timeout.
- **Delete a guard that can never fire.** The "alert a failed run once" check was dead, because a
  failed run is never executed again. Mutation showed it, and it was removed, not tested.

**How earlier lessons worked.**
- "Name the open decision when you close it" set the design right before any test. All four
  decisions are in its README.
- "Faults injected through `IAppDbContext`": `SaveFault`'s store switch failed a run at its store
  stage for the failed-run and retry tests, with no new fake.
- "A worker's schedule is proven by advancing `FakeTimeProvider`" carried over unchanged: the
  test waits for the 22-hour timer, advances it, then waits for the next day's 24-hour timer.


## Journey 20: Staff mutes an issue

Three slices:
- the mute and unmute endpoints, with each issue's `state` on a run;
- Mute, the Muted badge and Unmute in a run's issues, with the success toast and Undo;
- the failure toast with Try again, which reads the run again when the issue is over.

The mocks gained the muted run detail and the failed-mute toast before any code. The design
settled its open points first: the failure message, titles by kind alone, no badge refresh, and
`409 issue_closed` for an issue that is over.

What slowed it:
- Mute needed an accessible name that says which issue it acts on. The button's `label` input
  gives that name, but it also draws the button square as an icon button.
- A test that pinned an issue's exact keys failed when `state` was added. That failure was
  intended: a new field on a contract is a change to check.
- A mutant written against the code as typed did not apply: Prettier had rewrapped the line. The
  mutation loop reported `NO-CHANGE`, not a pass, so it was caught and run again.
- The API keys spec failed once while it ran beside the new spec on one port. It passed alone and
  in two full runs, so the cause is not known. Watch it before calling it flaky.

Lessons:
- **Give a text button its full name with `name`, not `label`.** `label` is the name of an
  icon-only button and draws it square. `name` names a button with visible text ("Mute “Source
  timed out” for Covenant Cycling Collective") and keeps its shape. Revoke and Edit source moved
  to it too.
- **Change the shown state from the API's answer, not before it.** The issue shows muted only
  once the API answers. A refusal then needs nothing undone, and a 409 reads the run again.
- **Have the mutation loop say when a mutant did not apply.** The `cmp` check turned a silent
  pass into `NO-CHANGE`. Write a mutant against the file as it is now, after the formatter has
  run.

**How earlier lessons worked.**
- "Name the open decision when you close it": the mute design's decisions were in its README
  before the first test, and nothing about them was asked again.
- "Prove an absence by exact keys": the pinned-keys test caught the new `state` field, as it
  should.
- "Read the initial total after each new Angular API": `afterNextRender` was already paid for,
  and the total stayed 253.61 kB.

## Journey 21: Staff monitors API usage

Six slices:
- the consumer API counts each `/v1` request per client and hour, and each client's row reads its
  last 24 hours;
- `GET /admin/v1/api-usage` with the base address (a new required setting) and p95 latency from
  bucket counts;
- the flush service that writes the counts every 60 s and on stop, keeping them when a save
  fails;
- the summary card and the usage columns on the API screen;
- the endpoints list and its example panel, with the `endpoint` and `code-panel` components;
- the loading skeletons, the usage-only error with Try again, and the pager.

The design's seven open points were settled in its README before the first test: what counts,
the flush interval, the latency buckets, the window, the error-rate rounding, and the two missing
mock states (`usage-error`, `paged`), which were added first.

What slowed it:
- The initial total moved from 253.61 to 253.65 kB in slice 4. Bisecting took eight builds and
  ended at three cell templates of the form `{{ row.x }}`. The stats showed no new input in
  `main`, only a few more bytes in Angular inputs it already had: the minifier's names had
  shifted. The growth was accepted, and the total stayed there through slices 5 and 6.
- In zsh, an unquoted `$FILES` is one word, not a list. A backup loop over it copied nothing, and
  the files were saved only by an earlier step's copies.
- The older API specs build their own client objects. Without the new usage fields,
  `last_call_at` was `undefined`, and the page threw "Invalid time value".
- The API keys failure seen once in journey 20 came back and could be reproduced. The test
  clicked back to API while Sources was still loading, which cancelled that visit, so the screen
  never left. A Settings test failed 2 times in 120 runs for a similar reason: it pressed Escape
  before the guard had opened its dialog.
- L2-067 puts the endpoints beside their example from 1024 px; the design-system pages said
  1280 px. The requirement won, and both pages were corrected.
- The 300 ms skeleton's cancel on an answer survived its mutant. It was redundant: `slow` matters
  only while loading, and each load resets it. The code was removed; no test was added for it.

Lessons:
- **A bundle growth with no new input in `main` is renaming, not code.** Before bisecting a growth
  under 0.1 kB, diff the stats inputs of `main` (`dist/admin/browser-stats.json`, paths cut at
  `node_modules/`). If no input was added and only existing ones changed by a few bytes, the
  minifier renamed things; record it and move on.
- The zsh loop that copied nothing repeats journey 17's lesson, "Use an array for a list of paths
  in a zsh loop". The lesson was known and not applied, so it is not a new one.
- **A new field on a DTO means grepping every fixture that builds it.** Search the specs for
  another field of the DTO (`revoked_at:`), not only the shared fixture.
- **Wait for the screen or dialog before acting away from it.** After a click that loads a lazy
  route or opens a dialog through a guard, assert its heading or focused control before the next
  click or key.
- **When a requirement and the design system disagree, follow the requirement and correct the
  design-system page in the same change.**
- **A mutant that survives may show redundant code.** Remove the code rather than write a test
  for it.

**How earlier lessons worked.**
- "Have the mutation loop say when a mutant did not apply" caught two more mutants against
  rewrapped lines. A run that exited 1 with no failed count turned out to be a compile error, not
  a killed mutant.
- "Diff `--stats-json` inputs against a base build" settled the bundle question. The stats file
  is `browser-stats.json`, not `stats.json`.
- "Watch the API keys spec before calling it flaky": watching it led to the real cause, and it
  was fixed rather than retried.

## Journey 22: Staff exports articles

Four slices:
- `GET /admin/v1/articles/export`, streaming the rows the list would show for the same filters,
  every value quoted and guarded against formulas;
- the refusal above 50,000 rows, `400 export_too_large`;
- "Export CSV" on the Articles screen, with "Export started", the saved file and the revoked
  object URL, and the states that disable it;
- the warning for too many rows, the failure with "Try again", and the export cancelled on
  leaving.

The design's six open points were settled first, mostly from the mocks: the button is disabled
while loading and in the error and empty states and enabled on no results; the path; empty cells
for missing values; no audit; and the two notifications, whose mocks (`article-toast/warning`,
`export-failed`) were added before any code.

What slowed it:
- The mock checker wants a state named for each tone. The too-large toast became `warning`, not
  `export-too-large`.
- An edit script failed its first assertion because Prettier had put an export list on one line.
  The script wrote nothing else, as it should, but the shell carried on and ran the specs against
  the half-made change.
- The outcome toast stacked under "Export started", and the page object's `toast` matched both.
  Pointing it at the newest made three older `toHaveCount(1)` checks pass however many toasts
  there were. They now count `toasts`.
- A mutant that leaves `file` possibly null did not compile. Writing it with a cast made it run,
  and it was caught.

Lessons:
- **Share a list's filter with its export through one interface.** `IAdminArticleFilter` lets the
  list and the export use one `Apply` and one validator, and the export's theory reuses the list
  tests' case table, so the two cannot drift.
- **Prove a bound on a stored store, not through collection.** Adding 50,001 articles straight to
  the InMemory store in a class fixture took under 4 s. Collecting them would have taken minutes.
- **When a locator narrows to one element, check what counted it.** `.last()` and `.first()` turn
  `toHaveCount(n)` into "at most one". Give counts their own locator.
- **Stop the shell when an edit script fails.** End the script's command with `|| exit` before
  anything that runs tests, or run the tests in their own call.
- **A redundant flush is code no test can see.** `StreamWriter` hands each full buffer on, so a
  flush every 500 rows added nothing. It was removed.

After the push, CI failed once in journey 21's flush test: the flush on stop saved nothing. It
did not fail in 40 runs alone or 6 full runs locally. Delaying the count in the middleware by
300 ms did not break the test either, so the first guess, that the client gets its answer before
the count, was wrong, and the barrier written for it was removed. Reading the flush handler found
a real loss: it drained the counts before two reads, and put them back only when the save failed.
A stop that cancels a timer flush between its drain and its save loses the counts, and the
cancellation is not logged. That fits the failure. A failed read is the deterministic case, so a
new test arms a read fault (red: 0 counted); the handler now puts the counts back on any failure
after the drain. The CI failure itself was not reproduced.

- **Put everything after a drain inside the restore.** A buffer taken before a read loses its
  contents when the read fails or is cancelled. Wrap every step from the take to the save.
- **Test a timing guess by forcing the timing.** A 300 ms delay showed that the client waited for
  the count. That killed the guess before a barrier for it was kept.

**How earlier lessons worked.**
- "Add a missing mock state, or record a stand-in, rather than invent": both export notifications
  had mocks before the code, and their copy was not argued over again.
- "An absence needs a barrier": the cancelled export's test checks after the Overview heading has
  shown, and the fixture's page-error check proves nothing was thrown.
- "Have the mutation loop say when a mutant did not apply" flagged two mutants: one run exited 1
  with no failed count (a compile error) and one reported `NO-CHANGE`.


## Journey 23: Worker applies retention

Five slices, backend only:
- articles collected before the cutoff, and the run's `deleted` count;
- events, and organizations that nothing still refers to;
- old runs with what hangs off them, closed issues with no occurrence left, and API usage after
  30 days;
- batches of 1,000, and what a failed batch keeps;
- the worker each night at 03:00 Toronto time.

The design's four open points were settled before any code:
- the 03:00 slot;
- the order of deletes (events before articles);
- `deleted` as the count's name;
- tracked batch deletes in place of `ExecuteDeleteAsync`.

Audit entries wait for journey 24, which creates them.

What slowed it:
- The order the design gave, articles then events, would have lost the events. Deleting an
  article sets its events' link to null, so the rule "an event goes with its expired article"
  could no longer find them. A mutant that put articles first proved it.
- Slice 4's tests passed at once, because slice 1 had already built the batches. They counted
  only after mutants on the batch size failed them.
- A mutant applied to the first of two identical predicates and survived. The first predicate
  only chose which issue alerts to delete, and the test had no alert for an open issue. A shared
  `Expression` for the two predicates now keeps them from drifting apart.
- A mutant that wrapped the run in a transaction failed every test, because InMemory has no
  transactions. That proves nothing about the batches, and it is not counted.

Lessons:
- **Delete what a set-null key points at before what it points to.** Otherwise the rule that
  finds the dependent no longer matches it.
- **Use a tracked batch delete where InMemory must run it.** Read at most 1,000, remove them and
  save. It deletes owned rows with their owner on every provider, and a save fault can count
  each batch.
- **Delete dependents in their own batches, through a subquery on the expiring owner.** The
  database's cascade then has nothing left to do, and InMemory, which cascades only tracked
  rows, leaves no orphans.
- **A mutant on a repeated predicate hits one copy.** Name the predicate once, or mutate each
  copy.
- **Share a daily slot between workers.** `DailySlot.NextAfter` now serves both the digest and
  retention, and each has its own case table across the changes of clock.

**How earlier lessons worked.**
- "An absence needs a barrier": each worker test waits for the timer of the next night, so the
  run it reads is the last one.
- "A redundant flush is code no test can see": the early return on a short batch saved one
  empty read, could not be seen, and was removed.
- "Prove a bound on a stored store": 2,500 articles added straight to the store kept the batch
  tests under a second each.

## Journey 24: Staff actions are audited

Six slices, backend only:
- decisions on articles (publish, hide, Undo), recorded only when something changed;
- sources (add, edit, remove, restore) and API keys (create, revoke), with no key in any entry;
- muting and unmuting issues, and starting and retrying runs;
- settings saves, by the names of the fields that changed;
- refused requests and failed sign-ins, counted in `keryx.auth.refused` and logged without the
  credential;
- retention deletes audit entries older than the period.

The design's open points were settled before any code:
- Undo, Unmute and Restore are audited;
- a retry records `run_retried` and `run_started`;
- the refusal reasons and the metric's unit and tags;
- operators read the table, so there is no read endpoint.

One point was changed, and the design now says why. The design had a pipeline behaviour write
each entry after its handler. That cannot keep the promise that an entry and its change are
saved together, because the handler has already saved. Each handler now calls
`IAuditTrail.Record` before its own `SaveChanges`, so one save holds both.

What slowed it:
- The first full run failed the refusal test. OpenTelemetry subscribes to a meter by its name,
  and the API tests run several hosts at once, so the exporter also counted other hosts'
  refusals. A `MeterListener` filtered on the host's own `IMeterFactory` scope counts only that
  host. A presence check on the exported metric still proves the exporter is wired.
- Removal mutants failed the build rather than a test, because the analyser rejects an unused
  constructor parameter. Writing `_ = audit;` in the mutant let them build.
- Two mutants survived until the tests grew:
  - recording local time survived until each entry was asserted to have offset zero;
  - comparing e-mail without case survived until a case-only row was added to the table.

Lessons:
- **Record an audit entry in the same save as the change.** A behaviour that runs after the
  handler cannot do it. A repeat that changes nothing then records nothing, without any extra
  check.
- **Count a metric per host by the meter factory's scope.** Exported metrics are shared by
  name across every host in the process.
- **Observe refusals in one middleware around authentication and authorization.** It sees every
  401 and 403, whichever handler made it, and it reads the route template from the endpoint.
  Only the sign-in callback, which has no endpoint, reports for itself.
- **Give a case table a row that only one comparison catches.** The case-only e-mail row is what
  makes an ordinal comparison testable.

**How earlier lessons worked.**
- "Delete what a set-null key points at first" did not apply: audit entries refer to nothing.
  They went last, through the same batch delete.
- "Prove an absence by exact keys": the API-key test lists every field of every entry, so no key
  or prefix can hide in one.
- "A redundant flush is code no test can see": the always-record mutant was equivalent, because
  an unsaved entry is thrown away, so nothing was removed for it.

Addendum: the push failed CI's format check on an import out of order in `StartRun.cs`. The
analysers pass on a misordered `using`, and only `dotnet format --verify-no-changes` catches
it. The fix was a one-line commit. Lesson: **run `dotnet format --verify-no-changes` with the
Release build before every push.** Journey 25 ran it before each commit.

## Journey 25: Operator observes the system

Four slices, backend only. Journey 1 had already built the shared wiring, the request spans,
`trace_id` and the admin app's "Reference":
- each worker run is one trace, with a span per stage and per source fetch;
- collection runs measure their duration, items, failed sources and fetch latency;
- consumer requests are measured and traced by route template and client;
- an unhandled exception is logged by its type and trace, never its message.

The design's open points were settled before any code. Three changed, and the design says why:
- **The root span starts in `RunRecordingBehavior`, not the pipeline.** A failed run's
  correlation id is then its trace's id. A pickup with nothing to run clears the span's
  recorded flag, so the pickup every 10 seconds exports nothing.
- **There is no `ApiMetrics` meter.** ASP.NET Core's `http.server.request.duration`, with
  `keryx.client.id` added through `IHttpMetricsTagsFeature`, gives the count, the latency and
  the errors by route and client without a second set of instruments.
- **There is no `HttpClient` instrumentation.** The fetcher drives its own sockets, and the
  `source.fetch` span, named by the source's id, is the fetch's span.

Durations are in seconds, as the OpenTelemetry conventions measure them.

What slowed it:
- Every telemetry test first passed alone and failed in the full suite. An `ActivitySource`, a
  meter that OpenTelemetry subscribes to by name, and ASP.NET Core's server spans are all heard
  by every host in the process. Each test now tells its own data apart:
  - metrics through a `MeterListener` filtered on its host's `IMeterFactory`
    (`HostMeasurements`);
  - spans by the run's or client's id.
- An "absence" check (no root span without a run id) would have caught other tests' failed
  pickups. It now excludes error spans, which are not what it proves.
- `SignallingTimeProvider.TimerAsync` waits for a timer but does not advance the clock. A
  second pickup needs `Advance` first.
- Fetches run eight at once, so a fake that advances the clock for one source can stretch
  another source's measured latency. Only the fed source's latency is asserted exactly; the
  other's is given a range.
- The log test's first matcher looked for the trace id in the body, while it sits in the
  attributes. Both mutants "died" only because the test could never pass, so they were run
  again after the fix.

Lessons:
- **In a process that runs several hosts, tell telemetry apart by what this test created.**
  Filter meters by factory scope, and spans by the ids the test seeded. Exporters are shared
  by name.
- **Prefer enriching the framework's instrument to adding a parallel one.**
  `IHttpMetricsTagsFeature` adds a tag to `http.server.request.duration` at no cost.
- **Never hand an exception to a logger whose lines are exported.** Log its type and a
  correlation id; the exporter would send its message and stack.
- **A mutant killed by a test that cannot pass proves nothing.** Rerun the mutants after the
  test goes green.

**How earlier lessons worked.**
- "Count a metric per host by the meter factory's scope" (journey 24) became the shared
  `HostMeasurements` helper. It was needed again the same day.
- "A presence check proves the exporter is wired": each meter's `AddMeter` has a mutant that a
  by-name export check kills.

## Issue 5: The Runs link counts failing sources

One slice, frontend only. `NavLink` already took a count and its accessible name, and the shell's
`ScheduleStore` already read `failing_sources`; the shell had never bound one to the other. The
badge now shows on every signed-in route and in the narrow top bar, through the new domain
helper `failingSourcesLabel`.

What slowed it:
- The shared fixture's schedule has 2 failing sources, so the badge appeared in every spec at
  once. The shell's critical test listed the links by text and now reads "Runs 2", which is
  what the mock shows. The Runs page object found its link by the exact name "Runs" and no
  longer matched; it now finds the link by its class and `href`.
- A first draft opened the badge spec on Sources and API, which read endpoints the shared
  fixture does not answer. The badge belongs to the shell, so routes the fixture answers prove
  it.

Lessons:
- **Find a navigation link by its `href`, never by its accessible name.** The name carries
  whatever the link counts.
- **A component input nobody binds is a gap the component's own tests cannot see.** Before
  building, check whether the library already takes the value and only the app is missing.

**How earlier lessons worked.**
- "A new default in a shared flow means running the whole suite" found the three Runs specs in
  one run.
