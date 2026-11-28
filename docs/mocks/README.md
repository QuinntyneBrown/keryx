# Keryx mocks

Static HTML mocks of the Keryx admin app: every page, dialog and notification, in every state,
responsive, in light and dark. They are the reference the admin app is built against.

**Source of the design.** Every screen comes from the Magic Patterns project
*Christian Fitness News Aggregator* (prototype *Keryx Admin*: Overview, Articles, Runs, Sources,
API, Settings; and *Keryx Brand* for the mark, colour and type). Layout, copy, data and tokens are
taken from that project. What these mocks add is listed under [What is not in the design](#what-is-not-in-the-design).

## How to open them

Open [index.html](index.html) from disk; no server or network is needed. Controls that change
state are links to the mock of that state, so the product can be clicked through.

- `?theme=dark` or `?theme=light` forces a theme; the mock bar toggles it, and so does the `t` key.
- `?chrome=0` hides the mock bar and the yellow notes, leaving only the product.
- Check each mock at 360, 768 and 1280 px wide. The sidebar becomes a top bar under 1024 px,
  dialogs become bottom sheets under 640 px, and the article detail panel covers the screen
  under 1024 px.

```text
docs/mocks/
  index.html            gallery (generated)
  manifest.json         the list of screens and states
  assets/tokens.css     imports ../design-system/tokens/tokens.css, the one token file
  assets/ui.css         imports ../design-system/assets/components.css, the one component kit
  assets/mock.css|js    the mock bar and notes; not product UI
  pages/<id>/<state>.html
  dialogs/<id>/<state>.html
  notifications/<id>/<state>.html
```

The tokens, the component kit and the fonts (Geist and Geist Mono, SIL Open Font License) live
in the [design system](../design-system/README.md), which was extracted from these mocks. A mock
and its design-system page render the same CSS; extend the kit there, never here.

## Cast and catalog

The same story runs through every mock. "Now" is **Today, 9:46 AM, Toronto time**; the next
scheduled collection is at 12:00 PM (in 2h 14m); collections run every 6 hours.

| What | Values |
|---|---|
| Signed-in staff | Daniel Adeyemi, Owner |
| Totals | 18,432 articles (6 need review, 17,911 published, 515 hidden) · 64 sources (2 failing, 1 needs a look) |
| Last run | `run_2418`, today 6:00 AM, 4m 12s: 47 new, 18 duplicates, 62 of 64 sources OK |
| Other runs | `run_2417` today 12:00 AM, clean, 12 new · `run_2414` yesterday 12:00 PM, failed, database unreachable · `run_2419` the manual run started at 9:46 AM in the *running* states |
| Open issues | Feed returned 404 (Redeemed Run Club Toronto) · Timed out after 30s, 4 runs in a row (Covenant Cycling Collective) · 6 articles held for review (Google News · "Toronto church fitness") |
| Organisations | Shepherd's 5K · Grace Barbell Club · Crossroads Church Fitness Ministry · Ontario Christian Coaches Network · Temple Fit Markham · Faithful Fuel Nutrition · Torch Athletics Brampton · Harbourfront Prayer Walk · Kingdom Strong CrossFit · Redeemed Run Club Toronto · Covenant Cycling Collective |
| Article under review | `art_18429` "Toronto youth pastors challenge congregations to 100 km in March", relevance 0.58 |
| Source being added | LifePoint Fitness Ministry, `https://lifepointfit.ca/events`, Scarborough |
| API consumers | Keryx Mobile (iOS & Android) · GTA Faith & Fitness newsletter · Kingdom Strong website widget · Temple Fit partner app · Old staging key (revoked) |
| Key being created | "Covenant Cycling site", `kx_live_R8vN…` |
| Thresholds | publish at 0.60 and above · review from 0.45 · hidden below 0.45 |

## What is not in the design

The Magic Patterns project shows each screen populated, with a few variants (the four pipeline
states of Overview, the article panel, the Add a source dialog, the new-key banner, the unsaved
changes bar). Everything below was derived from those screens so that no state is left to
invention during implementation. Each should be confirmed or redrawn in Magic Patterns.

- **Loading, empty, error, no-results and partial states** of all six pages.
- **Dialogs behind designed buttons**: Edit source and Remove source (the row "more" button and
  *Edit source* on Runs), New API key (the header button), Revoke API key (the design shows a
  revoked key but no control to revoke one, so each active row gains a *Revoke* button), and
  Add a topic (the *Add* chip in Settings; the repository rules forbid inline forms).
- **Busy, invalid and failed states** of every dialog, including Add a source.
- **Toasts** for Run now, Retry failed sources, Mute, Publish, Hide from API, Export CSV,
  adding and removing a source, and copying or revoking a key.
- **Paging** under the Articles and Sources tables. The design lists 10 of 18,432 articles and
  12 of 64 sources with no way to reach the rest; the API must page every list.
- **Dark theme.** The design is light only. Dark is derived from the same ink scale, with volt
  as the primary action colour.
- **Tab counts on Articles** are the real totals (18,432 / 6 / 17,911 / 515). The design counts
  its ten sample rows (10 / 1 / 8 / 1), which contradicts the "6 articles held for review" issue.
  Five more held articles were written for the *Needs review* tab.
- **The running state starts at 9:46 AM**, when Run now is pressed. The design says
  "Started 12:00 PM" while also showing the next run at 12:00 PM.

Deliberate departures from the design, for accessibility:

- Metadata text uses `#656c79` instead of the design's `#8a909b`, which is 3.2:1 on white and
  fails WCAG AA for text. Form control borders use `#8a909b` (3.2:1) instead of `#cbcfd7` (1.5:1).
- Controls are 36 px tall as designed with a fine pointer, and 44 px on touch devices.
- Issue cards on Overview show an arrow instead of the external-link icon: they open the run
  inside the app, not a new tab.
- Lists that are tables in meaning (articles, sources, API consumers) are real tables.

## Open questions

1. **Product scope.** `AGENTS.md` describes Keryx as holding events, organisations and churches,
   with workers that run daily searches, and an admin app that shows when each worker last ran
   and runs small queries. The design shows one collection pipeline every 6 hours, articles
   only, plus source and API key management and settings. These mocks follow the design. Which
   is right decides whether Events and Organisations screens and per-worker run history are
   missing.
2. **Sign-in and system pages.** The design has no sign-in, sign-out, session-expired, 403 or
   404 screen, and how staff authenticate is not decided, so none is mocked. The avatar in the
   sidebar has no menu.
3. **API consumers.** The design lists "Keryx Mobile (iOS & Android)" as the largest consumer.
   `AGENTS.md` says Keryx has no product of its own and names Christian Fitness as a consumer.
4. **Dates.** The design mixes "Mon, Mar 9" (a Monday in 2026) with 2025 timestamps in the API
   examples and article URLs. The copy is kept as designed.
5. **Roles.** Daniel Adeyemi is "Owner". No other role appears, so no read-only or forbidden
   state is mocked.
6. **Mute.** *Mute* on an issue is treated as immediate with Undo. Whether it mutes one issue,
   one source, or for how long is not designed.
7. **Export CSV** is treated as a background download of all 18,432 rows. A bounded export may
   be required.
8. **News query sources** exist in the list but the Add a source dialog offers only RSS,
   Website, Instagram and Eventbrite.

## Checks

```sh
python .claude/skills/writing-html-mocks/scripts/check_mocks.py docs/mocks --write   # coverage, gallery, structure
python .claude/skills/writing-html-mocks/scripts/screenshot_mocks.py docs/mocks      # 360/768/1280 × light/dark into .cache/
```

`--write` regenerates [index.html](index.html) and the section below from `manifest.json`.
Never commit `docs/mocks/.cache/`.

## Coverage

<!-- coverage:start -->

Legend: ✅ mock exists · ➖ not applicable (reason in manifest) · ❌ missing

### Pages

| Screen | default | loading | empty | error | healthy | running | failed | partial | selected | selected-published | selected-hidden | review | no-results | edge | muted | success | problems | detail-error | narrow-detail | filtered | menu | key-created | endpoint | usage-error | paged | dirty | invalid | submitting | sign-out-failed | save-failed | changed-elsewhere | busy | signed-out | session-expired | Requirements |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Overview (`overview`) | [✅](pages/overview/default.html) | [✅](pages/overview/loading.html) | [✅](pages/overview/empty.html) | [✅](pages/overview/error.html) | [✅](pages/overview/healthy.html) | [✅](pages/overview/running.html) | [✅](pages/overview/failed.html) | [✅](pages/overview/partial.html) |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-042`, `L2-043`, `L2-044`, `L2-045`, `L2-046`, `L2-047`, `L2-073` |
| Articles (`articles`) | [✅](pages/articles/default.html) | [✅](pages/articles/loading.html) | [✅](pages/articles/empty.html) | [✅](pages/articles/error.html) |  |  |  |  | [✅](pages/articles/selected.html) | [✅](pages/articles/selected-published.html) | [✅](pages/articles/selected-hidden.html) | [✅](pages/articles/review.html) | [✅](pages/articles/no-results.html) | [✅](pages/articles/edge.html) |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-048`, `L2-049`, `L2-050`, `L2-051`, `L2-052`, `L2-053`, `L2-054`, `L2-055` |
| Runs (`runs`) | [✅](pages/runs/default.html) | [✅](pages/runs/loading.html) | [✅](pages/runs/empty.html) | [✅](pages/runs/error.html) |  | [✅](pages/runs/running.html) | [✅](pages/runs/failed.html) |  |  |  |  |  | [✅](pages/runs/no-results.html) |  | [✅](pages/runs/muted.html) | [✅](pages/runs/success.html) | [✅](pages/runs/problems.html) | [✅](pages/runs/detail-error.html) | [✅](pages/runs/narrow-detail.html) |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-047`, `L2-056`, `L2-057`, `L2-058`, `L2-059`, `L2-060`, `L2-061` |
| Sources (`sources`) | [✅](pages/sources/default.html) | [✅](pages/sources/loading.html) | [✅](pages/sources/empty.html) | [✅](pages/sources/error.html) |  |  |  |  |  |  |  |  | [✅](pages/sources/no-results.html) |  |  |  |  |  |  | [✅](pages/sources/filtered.html) | [✅](pages/sources/menu.html) |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-062`, `L2-063`, `L2-066` |
| API (`api`) | [✅](pages/api/default.html) | [✅](pages/api/loading.html) | [✅](pages/api/empty.html) | [✅](pages/api/error.html) |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | [✅](pages/api/key-created.html) | [✅](pages/api/endpoint.html) | [✅](pages/api/usage-error.html) | [✅](pages/api/paged.html) |  |  |  |  |  |  |  |  |  | `L2-067` |
| Settings (`settings`) | [✅](pages/settings/default.html) | [✅](pages/settings/loading.html) | ➖ | [✅](pages/settings/error.html) |  |  |  |  |  |  |  |  |  |  |  | [✅](pages/settings/success.html) |  |  |  |  |  |  |  |  |  | [✅](pages/settings/dirty.html) | [✅](pages/settings/invalid.html) | [✅](pages/settings/submitting.html) | [✅](pages/settings/sign-out-failed.html) | [✅](pages/settings/save-failed.html) | [✅](pages/settings/changed-elsewhere.html) |  |  |  | `L2-041`, `L2-070`, `L2-071`, `L2-072` |
| Sign in (`sign-in`) | [✅](pages/sign-in/default.html) | ➖ | ➖ | [✅](pages/sign-in/error.html) |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | [✅](pages/sign-in/busy.html) | [✅](pages/sign-in/signed-out.html) | [✅](pages/sign-in/session-expired.html) | `L2-039`, `L2-041` |
| Access denied (`forbidden`) | [✅](pages/forbidden/default.html) | ➖ | ➖ | ➖ |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-040` |
| Not found (`not-found`) | [✅](pages/not-found/default.html) | ➖ | ➖ | ➖ |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  | `L2-040` |

### Dialogs

| Screen | default | busy | invalid | tested | failed | exclude | focus-area | Requirements |
|---|---|---|---|---|---|---|---|---|
| Add a source (`add-source`) | [✅](dialogs/add-source/default.html) | [✅](dialogs/add-source/busy.html) | [✅](dialogs/add-source/invalid.html) | [✅](dialogs/add-source/tested.html) | [✅](dialogs/add-source/failed.html) |  |  | `L2-064` |
| Edit source (`edit-source`) | [✅](dialogs/edit-source/default.html) | [✅](dialogs/edit-source/busy.html) | [✅](dialogs/edit-source/invalid.html) |  | [✅](dialogs/edit-source/failed.html) |  |  | `L2-065` |
| Remove source (`remove-source`) | [✅](dialogs/remove-source/default.html) | [✅](dialogs/remove-source/busy.html) | ➖ |  | [✅](dialogs/remove-source/failed.html) |  |  | `L2-065` |
| New API key (`new-api-key`) | [✅](dialogs/new-api-key/default.html) | [✅](dialogs/new-api-key/busy.html) | [✅](dialogs/new-api-key/invalid.html) |  | [✅](dialogs/new-api-key/failed.html) |  |  | `L2-068` |
| Revoke API key (`revoke-api-key`) | [✅](dialogs/revoke-api-key/default.html) | [✅](dialogs/revoke-api-key/busy.html) | ➖ |  | [✅](dialogs/revoke-api-key/failed.html) |  |  | `L2-069` |
| Add a topic (`add-keyword`) | [✅](dialogs/add-keyword/default.html) | ➖ | [✅](dialogs/add-keyword/invalid.html) |  |  | [✅](dialogs/add-keyword/exclude.html) | [✅](dialogs/add-keyword/focus-area.html) | `L2-072` |
| Leave Settings (`leave-settings`) | [✅](dialogs/leave-settings/default.html) | ➖ | ➖ |  |  |  |  | `L2-071` |

### Notifications

| Screen | info | success | warning | danger | stacked | with-action | mute-failed | Requirements |
|---|---|---|---|---|---|---|---|---|
| Collection toast (`collection-toast`) | [✅](notifications/collection-toast/info.html) | [✅](notifications/collection-toast/success.html) | [✅](notifications/collection-toast/warning.html) | [✅](notifications/collection-toast/danger.html) | [✅](notifications/collection-toast/stacked.html) |  |  | `L2-075`, `L2-076` |
| Article toast (`article-toast`) | [✅](notifications/article-toast/info.html) | [✅](notifications/article-toast/success.html) | ➖ | [✅](notifications/article-toast/danger.html) |  | [✅](notifications/article-toast/with-action.html) |  | `L2-052`, `L2-054`, `L2-075` |
| Source toast (`source-toast`) | ➖ | [✅](notifications/source-toast/success.html) | [✅](notifications/source-toast/warning.html) | [✅](notifications/source-toast/danger.html) |  | [✅](notifications/source-toast/with-action.html) |  | `L2-064`, `L2-065`, `L2-075` |
| Run toast (`run-toast`) | [✅](notifications/run-toast/info.html) | [✅](notifications/run-toast/success.html) | [✅](notifications/run-toast/warning.html) | [✅](notifications/run-toast/danger.html) |  |  | [✅](notifications/run-toast/mute-failed.html) | `L2-059`, `L2-060`, `L2-075` |
| API toast (`api-toast`) | ➖ | [✅](notifications/api-toast/success.html) | [✅](notifications/api-toast/warning.html) | [✅](notifications/api-toast/danger.html) |  |  |  | `L2-068`, `L2-069`, `L2-075` |

<!-- coverage:end -->
