import { Component, computed, DestroyRef, inject, Injector, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NOTIFIER, SOURCES_API, type SourceKind, type SourceListItem } from '@kx/api';
import {
  Btn,
  Empty,
  FilterChip,
  Health,
  KIND_ICONS,
  Icon,
  Menu,
  PageBody,
  PageHeader,
  Pagination,
  Skeleton,
  Source,
  Table,
  type FilterChipOption,
  type MenuItem,
  type TableColumn,
} from '@kx/components';
import {
  formatCount,
  formatSourceAddress,
  nothingRelevantNotice,
  sourceHealthLabel,
  SOURCE_KINDS,
  sourceKindLabel,
  sourceKindOf,
  sourceLastFetched,
  sourceSiteLink,
  sourcesNoResults,
  sourcesRange,
  sourcesSummary,
} from '@kx/domain';
import { ScheduleStore } from '../../schedule/schedule-store';
import { HttpSourcesApi } from '../../sources/http-sources-api';
import { SourcesStore } from '../../sources/sources-store';
import type { SourceAdded } from './add-source-dialog/add-source-dialog';
import type { RemoveSourceData } from './remove-source-dialog/remove-source-dialog';

/** What the header says when it cannot count the catalogue: while loading, or after a failure. */
const PURPOSE = 'Feeds, pages and queries Keryx checks on every run.';

/** Toronto's zone, which Keryx's times read in until the schedule names it. */
const TORONTO = 'America/Toronto';

/**
 * Sources (docs/mocks/pages/sources/). Journey 3 builds the header with "Add source", the empty
 * and error states, and a table of each source's name, address, health and region, failing
 * first. The kind chips, yield, last fetch, row menu and paging arrive with journey 17 (ADR 0008).
 */
@Component({
  selector: 'kx-sources',
  imports: [
    Btn,
    Empty,
    FilterChip,
    Health,
    Icon,
    Menu,
    PageBody,
    PageHeader,
    Pagination,
    Skeleton,
    Source,
    Table,
  ],
  providers: [{ provide: SOURCES_API, useClass: HttpSourcesApi }, SourcesStore],
  templateUrl: './sources.html',
  styleUrl: './sources.scss',
})
export default class Sources {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly schedule = inject(ScheduleStore);
  private readonly injector = inject(Injector);
  private readonly notifier = inject(NOTIFIER);
  protected readonly store = inject(SourcesStore);

  protected readonly columns: TableColumn[] = [
    { key: 'source', label: 'Source', primary: true },
    { key: 'health', label: 'Health', wide: true },
    { key: 'region', label: 'Region', wide: true, muted: true },
    { key: 'articles', label: 'Articles · 30d', wide: true, numeric: true },
    { key: 'lastFetched', label: 'Last fetched', wide: true, muted: true, nowrap: true },
    { key: 'actions', label: 'Actions', actions: true, hiddenLabel: true },
  ];

  /** The kind the URL narrows the list to, or null for every kind (L2-062 AC4). */
  protected readonly kind = signal<SourceKind | null>(null);

  /** All, then each kind, the one in force pressed. */
  protected readonly chips = computed((): FilterChipOption[] => {
    const kind = this.kind();
    return [
      { value: 'all', label: 'All', pressed: kind === null },
      ...SOURCE_KINDS.map((each) => ({
        value: each,
        label: sourceKindLabel(each),
        pressed: each === kind,
      })),
    ];
  });

  /** No source is of the kind chosen (L2-062 AC5); null otherwise. */
  protected readonly noResults = computed(() => {
    const state = this.store.state();
    const kind = this.kind();
    if (state.status !== 'ready' || !kind || state.page.total > 0) return null;
    return { ...sourcesNoResults(kind, state.page.all_count), icon: KIND_ICONS[kind] };
  });

  protected readonly description = computed(() => {
    const state = this.store.state();
    if (state.status !== 'ready') return PURPOSE;
    const { all_count: all, failing_count: failing, needs_look_count: needsLook } = state.page;
    return sourcesSummary({ all, failing, needsLook });
  });

  protected readonly rows = computed(() => {
    const state = this.store.state();
    if (state.status !== 'ready') return [];
    const now = this.schedule.now();
    const zone = this.schedule.schedule()?.time_zone ?? TORONTO;
    return state.page.items.map((item) => ({
      item,
      kindLabel: sourceKindLabel(item.kind),
      address: formatSourceAddress(item.kind, item.address),
      health: sourceHealthLabel(
        item.health,
        item.held_count,
        item.failure_reason,
        item.consecutive_failures,
      ),
      articles: formatCount(item.articles_30d),
      lastFetched: sourceLastFetched(item.last_fetched_at, now, zone),
      actions: rowActions(item),
    }));
  });

  /** "Showing 1–12 of 64 sources, failing first." (L2-062 AC1). */
  protected readonly range = computed(() => {
    const state = this.store.state();
    return state.status === 'ready'
      ? sourcesRange(state.page.page, state.page.page_size, state.page.total, this.kind())
      : '';
  });

  protected readonly hasNext = computed(() => {
    const state = this.store.state();
    return state.status === 'ready' && state.page.page * state.page.page_size < state.page.total;
  });

  protected readonly rowKey = (row: unknown) => (row as { item: SourceListItem }).item.id;

  /** Twelve rows of skeletons, a page, so the table does not shift when it arrives. */
  protected readonly skeletonRows = Array.from({ length: 12 }, (_, i) => ({ key: `s${i}` }));
  protected readonly skeletonKey = (row: unknown) => (row as { key: string }).key;

  constructor() {
    // The page is in the URL, so a reload or a shared link opens it.
    const subscription = this.route.queryParamMap.subscribe((params) => {
      const kind = sourceKindOf(params.get('kind'));
      this.kind.set(kind);
      void this.store.load(pageParam(params.get('page')), kind);
    });
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }

  /** A kind chip: the list shows that kind, or every kind, from its first page (L2-062 AC4). */
  protected choose(value: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { kind: sourceKindOf(value), page: null },
    });
  }

  /** A row menu's item was chosen (L2-063). */
  protected act(action: string, source: SourceListItem, table: Table): void {
    if (action === 'edit') void this.editSource(source);
    else if (action === 'test') void this.store.testSource(source);
    else if (action === 'remove') void this.removeSource(source, table);
  }

  /**
   * Opens "Remove source" once the source's article count is read (L2-065 AC3), with focus on
   * Cancel. Closed, focus returns to the row's menu button; removed, the row is gone, so focus
   * goes to the table.
   */
  private async removeSource(source: SourceListItem, table: Table): Promise<void> {
    const opener = document.activeElement;
    const [{ Dialog }, dialog, detail] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./remove-source-dialog/remove-source-dialog'),
      this.store.detail(source),
    ]);
    if (!detail) return;
    const data: RemoveSourceData = { source, articles: detail.articles };
    const ref = this.injector.get(Dialog).open<boolean>(dialog.RemoveSourceDialog, {
      injector: this.injector,
      data,
      ariaModal: true,
      ariaLabelledBy: dialog.REMOVE_SOURCE_TITLE_ID,
      ariaDescribedBy: dialog.REMOVE_SOURCE_DESCRIPTION_ID,
      autoFocus: dialog.REMOVE_SOURCE_AUTO_FOCUS,
      restoreFocus: false,
      backdropClass: 'kx-backdrop',
    });
    ref.closed.subscribe((removed) => {
      if (removed) table.focusCaption();
      else if (opener instanceof HTMLElement) opener.focus();
    });
  }

  /** Opens "Edit source" on a row's source (L2-065); saved, the page is read again in place. */
  private async editSource(source: SourceListItem): Promise<void> {
    const { openEditSource } = await import('../../sources/edit-source');
    if (await openEditSource(this.injector, source)) await this.store.refresh();
  }

  /** "Edit source" from the add warning: the new source, known by its id. */
  private async editAdded(id: string, name: string): Promise<void> {
    const { editSourceById } = await import('../../sources/edit-source');
    if (await editSourceById(this.injector, id, name)) await this.store.refresh();
  }

  /** Previous or Next: the page goes in the URL, which loads it. */
  protected step(direction: -1 | 1, page: number): void {
    const next = page + direction;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: next > 1 ? next : null },
      queryParamsHandling: 'merge',
    });
  }

  /**
   * Opens "Add a source" with this screen's injector, so it reaches the screen's API and store.
   * The CDK dialog and the dialog's code load when it is first opened, not with the screen
   * (ADR 0008). Focus starts in URL and returns to the pressed button when it closes.
   */
  protected async addSource(addButton: Btn): Promise<void> {
    const opener = document.activeElement;
    const [{ Dialog }, dialog] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./add-source-dialog/add-source-dialog'),
    ]);
    const ref = this.injector.get(Dialog).open<SourceAdded>(dialog.AddSourceDialog, {
      injector: this.injector,
      ariaModal: true,
      ariaLabelledBy: dialog.ADD_SOURCE_TITLE_ID,
      ariaDescribedBy: dialog.ADD_SOURCE_DESCRIPTION_ID,
      autoFocus: '#source-url',
      restoreFocus: opener instanceof HTMLElement ? opener : true,
      backdropClass: 'kx-backdrop',
    });
    ref.closed.subscribe((added) => {
      if (added) void this.added(added, addButton);
    });
  }

  /**
   * A source was added: the list shows it, a notification says so, and focus is on "Add source",
   * even when the dialog was opened from the empty state, which the list replaces (L2-064 AC5).
   * The next run's time joins the sentence when runs are scheduled (journey 4).
   */
  private async added(
    { id, organisation, kind, test }: SourceAdded,
    addButton: Btn,
  ): Promise<void> {
    const warning = nothingRelevantNotice(kind, test, organisation);
    this.notifier.notify(
      (warning && {
        ...warning,
        action: { label: 'Edit source', run: () => void this.editAdded(id, organisation) },
      }) ?? {
        tone: 'success',
        title: 'Source added',
        text: `${organisation} will be checked on the next run.`,
      },
    );
    await this.store.load();
    addButton.focus();
  }

  protected retry(): void {
    void this.store.load();
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}

/** The page the URL asks for; anything but a whole number from 1 is the first. */
function pageParam(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

/**
 * A row's menu: "Edit source", "Test fetch now", "Open <host>" for an http(s) address, then
 * "Remove source" (L2-063 AC1, AC4).
 */
function rowActions(source: SourceListItem): MenuItem[] {
  const site = sourceSiteLink(source.address);
  const open: MenuItem[] = site
    ? [{ id: 'open', label: `Open ${site.host}`, icon: 'external', href: site.url }]
    : [];
  return [
    { id: 'edit', label: 'Edit source', icon: 'pencil' },
    { id: 'test', label: 'Test fetch now', icon: 'refresh' },
    ...open,
    { id: 'remove', label: 'Remove source', icon: 'trash', danger: true },
  ];
}
