# Keryx design system

Extracted from [docs/mocks](../mocks/README.md) on 2026-11-27 with the `extracting-design-systems` skill. Open [index.html](index.html) locally; no server or network is needed. Press `t` to switch theme.

It is the reference the Angular components library is built from: each component page is named after its BEM block, which is also its folder under `frontend/projects/components/src/<name>/`. Class names are the mocks' class names; the e2e page objects locate by them.

```text
docs/design-system/
  index.html                 entry page
  tokens/tokens.css          source of truth: primitives, semantic tokens, light and dark
  tokens/tokens.json         DTCG export, generated
  tokens/contrast-pairs.json every fg/bg pairing the components rely on, checked
  assets/components.css      the component CSS, one BEM block per component
  assets/fonts/              Geist and Geist Mono, bundled (SIL Open Font License)
  assets/brand/              mark, logo for light and dark backgrounds, favicon
  assets/ds.css, ds.js       documentation chrome only; not product UI
  foundations/  components/  patterns/
```

## Brand

| | |
|---|---|
| Ink | `#0E1116` (ink 2 `#181C23`) |
| Volt | `#C6F135`: on ink or as a fill, never text on white |
| Paper | white `#FFFFFF`, app background `#F4F5F7` |
| Status | success `#15803D`, warning `#B45309`, danger `#C81E1E`, info `#1D4ED8` |
| Type | Geist and Geist Mono, self-hosted |
| Logo | [mark](assets/brand/keryx-mark.svg), [logo for light backgrounds](assets/brand/keryx-logo.svg), [logo for dark backgrounds](assets/brand/keryx-logo-dark.svg), [favicon](assets/brand/favicon.ico); rules on the [brand lockup](components/brand.html) page |

## Foundations

| Page | Covers |
|---|---|
| [Color](foundations/color.html) | Ink, volt, paper and status colours; roles for both themes; every contrast pair with live ratios. |
| [Typography](foundations/typography.html) | Geist and Geist Mono, the size scale, eighteen role shorthands. |
| [Spacing](foundations/spacing.html) | The 4 px scale, inside and between rules, fixed sizes of small parts. |
| [Layout](foundations/layout.html) | Breakpoints, the 4/8/12 grid, named widths, page templates. |
| [Elevation](foundations/elevation.html) | Shadows, the surface hierarchy, z-order. |
| [Shape](foundations/shape.html) | Radii and border widths. |
| [Motion](foundations/motion.html) | Durations, easings, what moves, reduced motion. |
| [Iconography](foundations/iconography.html) | The mark and logo; the icon sheet with one meaning per icon. |
| [Theming](foundations/theming.html) | How light and dark work, token tiers, adding a theme, tokens.json. |
| [Responsive](foundations/responsive.html) | Mobile-first rules, per-component behaviour, touch, testing matrix. |
| [Accessibility](foundations/accessibility.html) | The WCAG 2.2 AA commitments, the component contract, testing. |
| [Content](foundations/content.html) | Voice, mechanics, numbers and times, error and empty-state formulas, microcopy. |

## Components (59)

| Component | BEM block | Variants | States | Source mocks |
|---|---|---|---|---|
| **Actions** | | | | |
| [Button](components/btn.html) | `.btn` | Primary, default, ghost, danger, danger solid; Icon-only, two growing buttons sharing a row, block | Default, Hover, Focus, Active, Disabled, Busy | [pages/overview/default](../mocks/pages/overview/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/runs/default](../mocks/pages/runs/default.html) +3 |
| [Icon button](components/icon-btn.html) | `.icon-btn` | Close, row actions (menu button), dismiss | Default, Hover, Focus, Expanded, Disabled | [pages/sources/menu](../mocks/pages/sources/menu.html), [dialogs/add-source/default](../mocks/dialogs/add-source/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html) +1 |
| [Link](components/link.html) | a | Inline, standalone, small standalone, external | Default, Hover, Focus | [pages/overview/default](../mocks/pages/overview/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/sources/menu](../mocks/pages/sources/menu.html) +1 |
| [Menu](components/menu.html) | `.menu` | Open menu for a failing source | Default, Hover / focus, Disabled | [pages/sources/menu](../mocks/pages/sources/menu.html), [pages/sources/default](../mocks/pages/sources/default.html) |
| [Segmented control](components/segmented.html) | `.segmented` | Run filter; schedule setting | Default, Hover, Focus, Selected, Disabled group | [pages/runs/default](../mocks/pages/runs/default.html), [pages/runs/problems](../mocks/pages/runs/problems.html), [pages/settings/default](../mocks/pages/settings/default.html) +1 |
| **Inputs** | | | | |
| [Form field](components/field.html) | `.field` | With help, optional, and a group in a fieldset; Stacks to one column below 640 px | Default, With help, Invalid | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/invalid](../mocks/pages/settings/invalid.html), [dialogs/add-source/invalid](../mocks/dialogs/add-source/invalid.html) +1 |
| [Text field](components/input.html) | `.input` | Default, mono, medium, short with suffix, multi-line | Default, Hover, Focus, Invalid, Read-only, Disabled | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/invalid](../mocks/pages/settings/invalid.html), [dialogs/add-source/default](../mocks/dialogs/add-source/default.html) +2 |
| [Select](components/select.html) | `.select` | Full width in a field; auto width in a toolbar | Default, Hover, Focus, Invalid, Disabled | [pages/articles/default](../mocks/pages/articles/default.html), [dialogs/add-source/default](../mocks/dialogs/add-source/default.html), [dialogs/edit-source/busy](../mocks/dialogs/edit-source/busy.html) |
| [Search field and toolbar](components/search.html) | `.search` | Search with two filters, as on Articles | Empty, Filled, Focus, Disabled | [pages/articles/default](../mocks/pages/articles/default.html), [pages/articles/no-results](../mocks/pages/articles/no-results.html), [pages/articles/loading](../mocks/pages/articles/loading.html) |
| [Checkbox](components/check.html) | `.check` | A check list with descriptions, as in New API key | Unchecked, Checked, Focus, Disabled, Disabled checked | [dialogs/new-api-key/default](../mocks/dialogs/new-api-key/default.html), [dialogs/new-api-key/invalid](../mocks/dialogs/new-api-key/invalid.html), [dialogs/new-api-key/busy](../mocks/dialogs/new-api-key/busy.html) |
| [Choice cards (radio group)](components/choice-card.html) | `.choice-card` | Two columns below 640 px, four from 640 px | Default, Hover, Focus, Selected, Disabled | [dialogs/add-source/default](../mocks/dialogs/add-source/default.html), [dialogs/add-source/busy](../mocks/dialogs/add-source/busy.html), [dialogs/edit-source/default](../mocks/dialogs/edit-source/default.html) |
| [Switch](components/switch.html) | `.switch` | Two switches under one legend | Off, On, Hover, Focus, Disabled off, Disabled on | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/submitting](../mocks/pages/settings/submitting.html) |
| [Slider](components/range.html) | `.range` | The only slider in the product | Default, Focus, Disabled | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/dirty](../mocks/pages/settings/dirty.html) |
| [Chip](components/chip.html) | `.chip` | Values, a newly added value, and Add | Default, Hover, Focus | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/dirty](../mocks/pages/settings/dirty.html), [dialogs/add-keyword/default](../mocks/dialogs/add-keyword/default.html) |
| [Filter chips](components/filter-chip.html) | `.filter-chip` | Source types on the Sources screen | Default, Hover, Focus, Active (pressed) | [pages/sources/default](../mocks/pages/sources/default.html), [pages/sources/filtered](../mocks/pages/sources/filtered.html), [pages/sources/no-results](../mocks/pages/sources/no-results.html) |
| [Settings section (form layout)](components/settings-section.html) | `.settings-section` | One section. On the page the form is capped at --layout-form-max (896 px). | What the section shows | [pages/settings/default](../mocks/pages/settings/default.html), [pages/settings/invalid](../mocks/pages/settings/invalid.html), [pages/settings/loading](../mocks/pages/settings/loading.html) |
| [Save bar](components/save-bar.html) | `.save-bar` | Dirty, submitting, invalid, saved | Status text, Actions | [pages/settings/dirty](../mocks/pages/settings/dirty.html), [pages/settings/submitting](../mocks/pages/settings/submitting.html), [pages/settings/invalid](../mocks/pages/settings/invalid.html) +1 |
| **Navigation** | | | | |
| [App shell](components/app.html) | `.app` | The three arrangements, schematically. Open the mocks under Sources to see them live. | Class, Used by | [pages/overview/default](../mocks/pages/overview/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/settings/default](../mocks/pages/settings/default.html) +4 |
| [Sidebar and top bar](components/sidebar.html) | `.sidebar` | Sidebar from 1024 px, top bar below | What changes | [pages/overview/default](../mocks/pages/overview/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/overview/running](../mocks/pages/overview/running.html) +1 |
| [Navigation link](components/nav-link.html) | `.nav-link` | Current, default, with count | Default, Hover, Focus, Current | [pages/overview/default](../mocks/pages/overview/default.html), [pages/overview/healthy](../mocks/pages/overview/healthy.html), [pages/settings/default](../mocks/pages/settings/default.html) |
| [Brand lockup](components/brand.html) | `.brand` | Asset files; Home link on ink; plain lockup on a card (the word takes the text colour) | Default, Focus (link) | [pages/overview/default](../mocks/pages/overview/default.html), [pages/sign-in/default](../mocks/pages/sign-in/default.html), [pages/forbidden/default](../mocks/pages/forbidden/default.html) |
| [Page header](components/page-header.html) | `.page-header` | Title, live description and a primary action; Without actions | Description, Action | [pages/sources/default](../mocks/pages/sources/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/settings/default](../mocks/pages/settings/default.html) +1 |
| [Section head](components/section-head.html) | `.section-head` | With a link; with a count | Default, Hover, Focus | [pages/overview/default](../mocks/pages/overview/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/api/default](../mocks/pages/api/default.html) |
| [Tabs](components/tabs.html) | `.tabs` | Tabs with counts above their panel | Default, Hover, Focus, Selected, Disabled | [pages/articles/default](../mocks/pages/articles/default.html), [pages/articles/review](../mocks/pages/articles/review.html), [pages/articles/loading](../mocks/pages/articles/loading.html) |
| [Pagination and table footer](components/pagination.html) | `.pagination` | First page | Footer | [pages/articles/default](../mocks/pages/articles/default.html), [pages/sources/default](../mocks/pages/sources/default.html), [pages/articles/review](../mocks/pages/articles/review.html) |
| [Breadcrumb](components/breadcrumb.html) | `.breadcrumb` | Two levels: a screen and one of its records | Default, Hover, Focus, Current | none yet (core set) |
| [Skip link](components/skip-link.html) | `.skip-link` |  | Unfocused, Focused | [pages/overview/default](../mocks/pages/overview/default.html), [pages/sign-in/default](../mocks/pages/sign-in/default.html) |
| **Containers and overlays** | | | | |
| [Card](components/card.html) | `.card` | A run summary card; Flush card with a full-bleed strip | What the card shows | [pages/runs/default](../mocks/pages/runs/default.html), [pages/api/default](../mocks/pages/api/default.html), [pages/overview/default](../mocks/pages/overview/default.html) |
| [Dialog](components/dialog.html) | `.dialog` | Form dialog; Destructive confirmation: small panel, Cancel focused first | What changes | [dialogs/add-source/default](../mocks/dialogs/add-source/default.html), [dialogs/add-source/invalid](../mocks/dialogs/add-source/invalid.html), [dialogs/add-source/busy](../mocks/dialogs/add-source/busy.html) +7 |
| [Detail panel](components/detail-panel.html) | `.detail-panel` | Needs review: Publish and Hide from API | Badge, Alert, Actions | [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/articles/selected-published](../mocks/pages/articles/selected-published.html), [pages/articles/selected-hidden](../mocks/pages/articles/selected-hidden.html) +1 |
| [Tooltip](components/tooltip.html) | `.tooltip` | Text; text with a shortcut | Hidden, Visible | none yet (core set) |
| [Divider](components/divider.html) | `.divider` | Horizontal, vertical, labelled | Default | none yet (core set) |
| [Stack, cluster, rail and split](components/stack.html) | `.stack` | .stack--sm, .stack, .stack--lg, .stack--xl; Cluster modifiers; Rail and split | Default | [pages/overview/default](../mocks/pages/overview/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/api/default](../mocks/pages/api/default.html) +1 |
| **Data display** | | | | |
| [Table](components/table.html) | `.table` | Articles: title link, context, a relevance meter and a status badge; Sources: an actions column; a revoked row struck through | Looks like | [pages/articles/default](../mocks/pages/articles/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/articles/loading](../mocks/pages/articles/loading.html) +3 |
| [List](components/list.html) | `.list` | Static rows; Run history: status icon, time, result; the open run is current; An issue in a run | Default, Hover, Focus, Current | [pages/overview/default](../mocks/pages/overview/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/runs/problems](../mocks/pages/runs/problems.html) +1 |
| [Description list](components/dl.html) | `.dl` | An article's facts | Default, Missing value | [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/articles/selected-published](../mocks/pages/articles/selected-published.html) |
| [Avatar](components/avatar.html) | `.avatar` | In the sidebar footer | Default | [pages/overview/default](../mocks/pages/overview/default.html) |
| [Badge](components/badge.html) | `.badge` | Neutral, success, warning, danger, info with dots; brand; Tag-like neutral badges, a plain warning badge, counts | Plain, With dot | [pages/articles/default](../mocks/pages/articles/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/api/key-created](../mocks/pages/api/key-created.html) +1 |
| [Tag](components/tag.html) | `.tag` | The scopes of a key | Default, In a revoked row | [pages/api/default](../mocks/pages/api/default.html) |
| [Health](components/health.html) | `.health` | Healthy, warning, danger, paused | On surface, On hovered row | [pages/sources/default](../mocks/pages/sources/default.html), [pages/sources/filtered](../mocks/pages/sources/filtered.html) |
| [Stat](components/stat.html) | `.stat` | Large values and a small value with a sub line, as in the status card; .stats--4.stats--ruled under a card head | Default, Danger, Loading | [pages/overview/default](../mocks/pages/overview/default.html), [pages/runs/default](../mocks/pages/runs/default.html), [pages/api/default](../mocks/pages/api/default.html) |
| [Meter](components/meter.html) | `.meter` | Published (≥ 0.60), needs review (0.45–0.60), hidden (&lt; 0.45) | Meter | [pages/articles/default](../mocks/pages/articles/default.html), [pages/articles/selected](../mocks/pages/articles/selected.html) |
| [Source cell](components/source.html) | `.source` | The five source types | Default | [pages/sources/default](../mocks/pages/sources/default.html), [pages/sources/loading](../mocks/pages/sources/loading.html) |
| [Status card](components/status-card.html) | `.status-card` | Default: completed with 2 failing sources; Running | Disc, Headline, Extra | [pages/overview/default](../mocks/pages/overview/default.html), [pages/overview/healthy](../mocks/pages/overview/healthy.html), [pages/overview/running](../mocks/pages/overview/running.html) +2 |
| [Summary card](components/summary-card.html) | `.summary-card` | API usage | Shows | [pages/api/default](../mocks/pages/api/default.html), [pages/api/loading](../mocks/pages/api/loading.html), [pages/api/empty](../mocks/pages/api/empty.html) |
| [Issue card](components/issue-card.html) | `.issue-card` | Two errors and a warning | Default, Hover, Focus | [pages/overview/default](../mocks/pages/overview/default.html), [pages/overview/healthy](../mocks/pages/overview/healthy.html), [pages/overview/loading](../mocks/pages/overview/loading.html) |
| [Run chart](components/run-chart.html) | `.run-chart` | Completed runs; The fifteenth bar pulses while it grows | Colour, Meaning | [pages/overview/default](../mocks/pages/overview/default.html), [pages/overview/running](../mocks/pages/overview/running.html), [pages/overview/failed](../mocks/pages/overview/failed.html) |
| [Timeline](components/timeline.html) | `.timeline` | A run that completed with issues | Step | [pages/runs/default](../mocks/pages/runs/default.html), [pages/runs/success](../mocks/pages/runs/success.html), [pages/runs/failed](../mocks/pages/runs/failed.html) +1 |
| [Endpoint row](components/endpoint.html) | `.endpoint` | Three endpoints, the first selected | Default, Hover, Current | [pages/api/default](../mocks/pages/api/default.html), [pages/api/endpoint](../mocks/pages/api/endpoint.html) |
| [Code panel](components/code-panel.html) | `.code-panel` | Request and response for /v1/status | Default, Block focused | [pages/api/default](../mocks/pages/api/default.html), [pages/api/endpoint](../mocks/pages/api/endpoint.html) |
| [Copy field](components/copy-field.html) | `.copy-field` | Inside the key-created callout | Default, Copied | [pages/api/key-created](../mocks/pages/api/key-created.html), [notifications/api-toast/success](../mocks/notifications/api-toast/success.html) |
| [Icon](components/icon.html) | `.icon` | Success, warning, danger, info, muted, inherited | Icon | [pages/overview/default](../mocks/pages/overview/default.html), [pages/sources/default](../mocks/pages/sources/default.html), [pages/runs/default](../mocks/pages/runs/default.html) |
| **Feedback** | | | | |
| [Toast](components/toast.html) | `.toast` | Info with Undo, success, warning with a link, danger (no countdown) | Behaviour | [notifications/collection-toast/info](../mocks/notifications/collection-toast/info.html), [notifications/collection-toast/success](../mocks/notifications/collection-toast/success.html), [notifications/collection-toast/warning](../mocks/notifications/collection-toast/warning.html) +7 |
| [Alert](components/alert.html) | `.alert` | Info, success, warning, danger with a title, neutral; Error summary; With actions | Alert, Used for | [pages/articles/selected](../mocks/pages/articles/selected.html), [pages/settings/invalid](../mocks/pages/settings/invalid.html), [dialogs/add-source/tested](../mocks/dialogs/add-source/tested.html) +5 |
| [Callout](components/callout.html) | `.callout` | Key created | Shows | [pages/api/key-created](../mocks/pages/api/key-created.html) |
| [Progress bar](components/progress.html) | `.progress` | Determinate with its sentence; indeterminate | Start, Part way, Complete, Indeterminate | [pages/overview/running](../mocks/pages/overview/running.html), [notifications/collection-toast/info](../mocks/notifications/collection-toast/info.html) |
| [Spinner](components/spinner.html) | `.spinner` | In a status disc, in a busy button, inline with text | Spinning, Reduced motion | [pages/overview/running](../mocks/pages/overview/running.html), [dialogs/add-source/busy](../mocks/dialogs/add-source/busy.html), [pages/sign-in/busy](../mocks/pages/sign-in/busy.html) |
| [Skeleton](components/skeleton.html) | `.skeleton` | Title, stat, circle, icon, tile, pill; text lines and chart; Composed | Shimmering, Reduced motion | [pages/overview/loading](../mocks/pages/overview/loading.html), [pages/articles/loading](../mocks/pages/articles/loading.html), [pages/runs/loading](../mocks/pages/runs/loading.html) +3 |
| [Empty state and system page](components/empty.html) | `.empty` | First run; No results and success (compact); With a reference id; Session expired: lockup, title, text, a start-aligned alert, one action | Variant, Action | [pages/overview/empty](../mocks/pages/overview/empty.html), [pages/overview/error](../mocks/pages/overview/error.html), [pages/articles/no-results](../mocks/pages/articles/no-results.html) +12 |

### Where the catalog's components live

The skill's catalog names components generically; Keryx pages are named after the BEM block. A catalog entry that is a variant of a block is documented on that block's page rather than duplicated.

| Catalog component | Page |
|---|---|
| Button | [Button](components/btn.html) |
| Button group | [Segmented control](components/segmented.html) |
| Link | [Link](components/link.html) |
| Menu | [Menu](components/menu.html) |
| Text field, textarea | [Text field](components/input.html) |
| Select | [Select](components/select.html) |
| Checkbox, plain radio | [Checkbox](components/check.html) |
| Radio group (cards) | [Choice cards (radio group)](components/choice-card.html) |
| Switch | [Switch](components/switch.html) |
| Slider | [Slider](components/range.html) |
| Form field, inline message | [Form field](components/field.html) |
| Form layout | [Settings section (form layout)](components/settings-section.html) |
| Top bar / app header, sidebar navigation | [Sidebar and top bar](components/sidebar.html) |
| Tabs | [Tabs](components/tabs.html) |
| Breadcrumb | [Breadcrumb](components/breadcrumb.html) |
| Pagination | [Pagination and table footer](components/pagination.html) |
| Skip link | [Skip link](components/skip-link.html) |
| Card | [Card](components/card.html) |
| Dialog | [Dialog](components/dialog.html) |
| Drawer / sheet | [Detail panel](components/detail-panel.html) |
| Tooltip | [Tooltip](components/tooltip.html) |
| Divider | [Divider](components/divider.html) |
| Container / grid / stack | [Stack, cluster, rail and split](components/stack.html) |
| Table | [Table](components/table.html) |
| List | [List](components/list.html) |
| Description list | [Description list](components/dl.html) |
| Avatar | [Avatar](components/avatar.html) |
| Badge / status | [Badge](components/badge.html) |
| Chip / tag | [Chip](components/chip.html) |
| Stat / KPI | [Stat](components/stat.html) |
| Code block | [Code panel](components/code-panel.html) |
| Timeline | [Timeline](components/timeline.html) |
| Toast | [Toast](components/toast.html) |
| Alert / banner | [Alert](components/alert.html) |
| Progress bar | [Progress bar](components/progress.html) |
| Spinner | [Spinner](components/spinner.html) |
| Skeleton | [Skeleton](components/skeleton.html) |
| Empty state, error page | [Empty state and system page](components/empty.html) |

Not built because no mock needs them and they are not in the core set: combobox, date picker, file upload, stepper, footer, command palette, accordion, popover.

## Patterns

| Pattern | Covers |
|---|---|
| [Forms](patterns/forms.html) | Dialog forms and the settings page: layout, validation timing, error summary, submit states. |
| [Feedback and loading](patterns/feedback.html) | Which feedback for which event; skeleton, spinner or progress; optimistic updates and undo. |
| [Empty and error states](patterns/empty-states.html) | First run, no results, error, and the system pages for sign in, access denied and not found. |
| [Navigation and page structure](patterns/navigation.html) | The shell, six flat screens, page header, panels and dialogs instead of sub-pages. |
| [Dialogs and overlays](patterns/dialogs.html) | Dialog, detail panel, menu, toast or tooltip; stacking; sheets on phones. |
| [Data tables and lists](patterns/tables.html) | Tables and lists: filtering, single selection, row actions, server paging, the small-screen strategy. |
| [Notifications](patterns/notifications.html) | Toasts, the navigation badge, the Overview and e-mail alerts; priority and timing. |
| [Content and tone](patterns/content.html) | One story told at five sizes in one voice; vocabulary and status words. |

## Tokens

- [`tokens/tokens.css`](tokens/tokens.css): the source of truth. Primitives (`--palette-*`, `--font-*`), semantic tokens, the dark theme (twice: `[data-theme="dark"]` and `prefers-color-scheme`; keep the two blocks identical), `prefers-contrast` and reduced-motion overrides, and the `@font-face` rules for the bundled fonts.
- [`tokens/tokens.json`](tokens/tokens.json): DTCG export, generated; never edit by hand.
- [`tokens/contrast-pairs.json`](tokens/contrast-pairs.json): the pairs `check_contrast.py` checks in both themes.
- [`assets/components.css`](assets/components.css): component tokens (`--btn-*`, `--alert-*`, `--switch-*`, `--brand-*`, `--select-*`) alias semantic tokens.

Standard slots filled for completeness and not used by any mock yet: `--font-size-5xl`, `--text-display`, `--shadow-2`, `--shadow-focus`, `--ease-spring`, `--z-popover`, `--color-info-border` and the other status borders. `--layout-topbar-height` is not defined: the top bar is not fixed-height or sticky.

## Drift found in the mocks

What the extraction normalised or flagged. “Mocks not rewritten” means the documented markup differs from the mock's and the product follows the documentation.

| Mock | Issue | Resolution |
|---|---|---|
| assets/tokens.css | Control edge `#8a909b` was 3.2:1 on white but 2.94:1 on the page background, where the Settings fields, the Articles toolbar and the off switch sit (WCAG 1.4.11 needs 3:1). | `--palette-ink-400` is now `#808691`: 3.4:1 on the page, 3.7:1 on white. |
| assets/tokens.css | Ten semantic tokens held raw hex values instead of aliasing a primitive (`#b9bdc6`, `#b4b8c0`, `#9399a3`, `#116a32`, `#11151a`, `#a9b0bc`, `#8f96a2`, `#5b616c`, `#747b88`, `#07090c`). | Added primitives ink-250, ink-350, ink-450, ink-940, ink-990 and green-800; every semantic token now aliases a primitive. |
| assets/tokens.css | Near-duplicate greys: sidebar muted text `#b4b8c0` vs toast muted text `#b9bdc6`; dark muted text `#a9b0bc` vs ink-300 `#a0a6b0`; dark subtle text `#8f96a2` vs sidebar subtle `#9399a3`; dark disabled `#5b616c` vs ink-600 `#555c69`. | Collapsed to one primitive each (ink-250, ink-300, ink-350, ink-600). All pairs still pass; the visible change is a few levels of grey. |
| assets/ui.css (`.brand`) | The sidebar wordmark was 24 px beside a 28 px mark (0.857 ×); the brand rule is 0.9 ×. | The wordmark size is derived from the mark: `calc(var(--brand-mark-size) * 0.9)` = 25.2 px. Lockup modifiers and fixed brand colour tokens were added. |
| assets/ui.css (`.skeleton--text`) | A 12 px bar with an off-grid 5 px margin made each skeleton line 22 px, one pixel taller than the 21 px text line it stands for. | The margin is derived from the line height, so skeleton and text are the same height. Loading mocks are a few pixels shorter. |
| assets/ui.css (`.timeline`, `.select`) | Off-grid raw offsets: 0.5625rem and 2.25rem for the timeline connector; 0.6875rem and 0.3125rem for the select caret. | Computed from icon-size and spacing tokens; they land on the same pixels. |
| assets/ui.css (`.segmented`, `.choice-card`) | Disabled opacity was 0.6 here and 0.5 on buttons, icon buttons and switches. | One token, `--opacity-disabled` (0.5). |
| assets/ui.css (animations) | Raw durations: `0.7s` spinner (twice), `1.6s` running-bar pulse. | `--duration-spin` (700 ms) added; the pulse uses `--duration-deliberate` (1.4 s). |
| assets/ui.css (type) | Three fonts composed in place: 18 px semibold (dialog title, panel title, small stat) and 16 px mono medium (summary value). | Role tokens `--text-title`, `--text-stat-sm`, `--text-code-lg`. |
| assets/ui.css (sizes) | About forty raw rem values for fixed parts (dots, avatar, tiles, chart, dialog, toast and menu widths, term column, skeleton shapes) and `1px` hairlines. | `--size-*` and `--layout-*` tokens, component tokens for the switch and select, and `--border-width-hairline` throughout. No rendered size changed. |
| assets/ui.css (`.code-panel`) | A focused code block showed the page's ink focus ring on the ink panel in the light theme: invisible. | The panel sets `--color-focus-ring` to volt, as the sidebar does. |
| assets/ui.css (`.filter-chip`, `.table tr`) | Only `aria-current="true"` was styled on filter chips, and only `aria-selected="true"` on rows (invalid on a `<tr>` outside a grid). | `aria-pressed="true"` (chips) and `aria-current="true"` (rows) are styled too and are the documented markup; the mock hooks still work. |
| 88 mocks (`.nav-link__badge`) | The count's meaning is an `aria-label` on a `<span>`, which has no role, so it is not reliably announced. | Documented markup: the digit in `aria-hidden` plus visually hidden text. Mocks not rewritten. |
| all pages and dialogs | Actions are `<a class="btn" role="button" href>` and tabs, segments and filter chips are links, so states can be clicked through. A link with `role="button"` does not answer Space. | A mock device, kept. The component pages document `<button>`, `role="tab"` buttons and `aria-pressed`. |
| pages/*, dialogs/*, notifications/* | Inline `style` attributes patch layout: `gap` on 141 clusters, `flex-wrap: nowrap; min-width: 0` on 68, `margin-top`/`margin-bottom` on 113 elements, a font weight on 19, a long alignment override on 19, `flex: 1` on 13. | Utilities added (`.cluster--xs/--sm/--md/--nowrap/--top/--fill`, `.flow-sm/-md/-lg`, `.text-regular`) and `.section-head` documented for heading spacing. Mocks not rewritten; the product must not use inline styles. |
| pages/overview/*, pages/sources/* | `title` attributes act as tooltips on run-chart bars and source-type icons; they do not show on focus or touch (hidden text makes them pass). | A real `tooltip` component is defined; the chart and source pages require the hidden text. |
| notifications/collection-toast/stacked | A `role="alert"` toast is nested inside the `role="status"` region. | Documented as two regions, one polite and one assertive. |
| pages/sign-in/*, pages/forbidden/default | A shell with no sidebar put `<main>` in the 240 px sidebar grid track from 1024 px; an alert inside the centred card inherited centred text. | `.app--bare`, `.page-body--centered` and `text-align: start` on `.alert__body` were added to `components.css`. |
| pages/sign-in/* (dark theme) | On the dark card the default lockup tile (`--color-shell-raised`) equals the card surface, so the tile disappears and only the strokes show. | Accepted for now and recorded; use `.brand--on-ink` on a dark surface if the tile must show. |
| pages/overview/running, notifications/collection-toast/info | The volt progress bar and running chart bar are about 1.3:1 on light surfaces. | Recorded as decorative in `contrast-pairs.json`: the value is always in adjacent text and in `aria-valuenow`. An ink fill in the light theme would fix it at the cost of the brand colour; left as a design decision. |
| assets/ui.css (unused) | `.btn--danger` (outline), `.health--paused` and `.chip--new` are defined but no mock uses them. | Kept and documented, each flagged on its page. |
| core set | Breadcrumb, tooltip and divider are in the core set but in no mock (the admin is flat; hairlines belong to other blocks). | Built from the same tokens and marked “new, not in the mocks yet”. |

## Closing the loop with the mocks

`docs/mocks/assets/tokens.css` and `docs/mocks/assets/ui.css` are now one-line `@import`s of `tokens/tokens.css` and `assets/components.css`, so a mock and its design-system page render the same CSS and there is one source of truth. `docs/mocks/assets/fonts/` is no longer referenced (the fonts load from `assets/fonts/` here) and can be removed.

## Verification

Run on 2026-11-27, from the repository root. Exit status is judged directly, not through a pipe.

| Command | Result |
|---|---|
| `python .claude/skills/extracting-design-systems/scripts/check_design_system.py docs/design-system` | exit 0 · 80 pages (59 components, 8 patterns), 0 errors, 0 warnings |
| `python .claude/skills/extracting-design-systems/scripts/check_contrast.py docs/design-system/tokens/tokens.css` | exit 0 · 192 passed, 0 failed, 0 unresolved (96 pairs × light and dark) |
| `python .claude/skills/extracting-design-systems/scripts/tokens_to_json.py docs/design-system/tokens/tokens.css` | wrote `tokens.json` (319 tokens) |
| `python .claude/skills/writing-html-mocks/scripts/check_mocks.py docs/mocks` | exit 0 · 20 screens, 95 mocks, 0 errors, 0 warnings (with the mocks reading this system's CSS) |

Screenshots (Chromium through Playwright, full page, reduced motion):

- Every design-system page (80) at 360 × 800 and 1280 × 800 in both themes: 320 captures, no page error, no failed request, no horizontal page overflow. Reviewed by eye: `index.html`, the colour page, and the button, dialog, table, brand and tabs pages, plus the forms and empty-states patterns. Fixed as a result: anatomy callouts covering small specimens, index card previews, button chrome on `<button class="tab">`, hidden text widening the page inside scrolling specimens, an ink-on-dark lockup.
- Every mock (95) at 360, 768 and 1280 px in both themes, before (the mocks' own `tokens.css` and `ui.css`) and after (this system's CSS): 570 captures each, no overflow. The 21 new sign-in, access-denied and not-found captures differ by design (they need `.app--bare` and `.page-body--centered`, which only this system has). The 36 loading captures are 6 to 27 px shorter (one pixel per skeleton line). All others have the same dimensions and differ only by the corrections in the drift log: the wordmark is 1.2 px larger, control edges and a few greys are slightly darker.

Not verified: no screen reader was run on these pages, and the Angular components library does not exist yet, so nothing here has been checked against real components or e2e locators.

## Changelog

- 2026-11-27: first extraction.
