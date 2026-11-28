import {
  Component,
  computed,
  DestroyRef,
  DOCUMENT,
  ElementRef,
  inject,
  Injector,
  signal,
} from '@angular/core';
import type { DialogRef } from '@angular/cdk/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiProblem,
  ARTICLES_API,
  NOTIFIER,
  type ArticleDecision,
  type ArticleRow,
  type ArticleStatus,
} from '@kx/api';
import {
  Badge,
  Btn,
  Empty,
  Icon,
  Meter,
  PageBody,
  PageHeader,
  Pagination,
  Search,
  Select,
  Skeleton,
  Table,
  Tabs,
  Toolbar,
  type SelectOption,
  type TabItem,
  type TableColumn,
} from '@kx/components';
import {
  ARTICLE_CATEGORIES,
  articleQueryFromParams,
  articleReasonAlert,
  articleQueryToParams,
  articleStatusLabel,
  articleStatusTone,
  categoryName,
  changeArticleQuery,
  formatCollectedAt,
  formatCount,
  formatRelativeAge,
  formatRelevance,
  noResultsWords,
  organizationLabel,
  pageRange,
  safeHttpUrl,
  searchTerm,
  titleLabel,
  type ArticleParams,
  type ArticleQuery,
  decidedNotice,
  decisionFailedNotice,
  exportFailedNotice,
  exportFileName,
  exportStartedNotice,
  exportTooLargeNotice,
} from '@kx/domain';
import { ArticlesStore } from '../../articles/articles-store';
import { HttpArticlesApi } from '../../articles/http-articles-api';
import { saveFile } from '../../articles/save-file';
import { ArticlePanel, type ArticlePanelView } from './article-panel/article-panel';

/** Below this width an open article covers the screen in a modal dialog (L2-051 AC2). */
const NARROW = '(max-width: 63.99rem)';

/** Each status tab's element id suffix: `tab-review` labels the panel for "Needs review". */
const TAB_IDS: Record<'all' | ArticleStatus, string> = {
  all: 'all',
  needs_review: 'review',
  published: 'published',
  hidden: 'hidden',
};

const PARAMS = ['status', 'q', 'region', 'category', 'page', 'article'] as const;

/**
 * Articles (docs/mocks/pages/articles/): the status tabs with the server's totals, one server
 * page of every collected article, newest collected first, and the pager. The URL holds the list
 * (L2-049 AC2, L2-050 AC6), so Back, a reload and a shared link show the same one.
 */
@Component({
  selector: 'kx-articles',
  imports: [
    ArticlePanel,
    Badge,
    Btn,
    Empty,
    Icon,
    Meter,
    PageBody,
    PageHeader,
    Pagination,
    Search,
    Select,
    Skeleton,
    Table,
    Tabs,
    Toolbar,
  ],
  providers: [{ provide: ARTICLES_API, useClass: HttpArticlesApi }, ArticlesStore],
  templateUrl: './articles.html',
  styleUrl: './articles.scss',
})
export default class Articles {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly notifier = inject(NOTIFIER);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  /** The open article's dialog below 1024 px, and the article it shows. */
  private dialog: { ref: DialogRef<unknown, ArticlePanel>; id: string } | null = null;
  private readonly media = matchMedia(NARROW);
  /** Whether the screen is narrower than 1024 px, where the panel is a modal dialog. */
  protected readonly narrow = signal(this.media.matches);
  /** The staff member's zone, in which collection times read (mocks: "Today, 6:03 AM"). */
  private readonly timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  protected readonly store = inject(ArticlesStore);
  protected readonly query = signal<ArticleQuery>(articleQueryFromParams({}));
  /** The table whose caption takes focus when the page asked for has loaded (L2-048 AC2). */
  private focusAfterLoad: Table | null = null;

  protected readonly columns: TableColumn[] = [
    { key: 'article', label: 'Article', primary: true },
    { key: 'category', label: 'Category', wide: true, muted: true },
    { key: 'region', label: 'Region', wide: true, muted: true },
    { key: 'relevance', label: 'Relevance', wide: true },
    { key: 'status', label: 'Status', wide: true },
  ];

  /** Whether the panel takes focus when it shows: it does when a person opened it. */
  protected readonly focusPanel = signal(false);

  /**
   * The open article as the panel shows it, in a list of at most one, so a different article
   * draws a new panel, which takes focus when a person opened it.
   */
  protected readonly panel = computed<ArticlePanelView[]>(() => {
    const detail = this.store.detail();
    if (!detail) return [];
    const regions = new Map(this.store.regions().map((region) => [region.slug, region.name]));
    const now = new Date();
    return [
      {
        id: detail.id,
        detail,
        title: titleLabel(detail.title),
        status: articleStatusLabel(detail.status),
        tone: articleStatusTone(detail.status),
        url: safeHttpUrl(detail.url),
        organization: organizationLabel(detail.organization),
        source: detail.source?.name ?? detail.provenance[0]?.source_name ?? 'Unknown',
        category: categoryName(detail.category),
        region: regions.get(detail.region) ?? detail.region,
        published: detail.published_at ? formatRelativeAge(detail.published_at, now) : 'Unknown',
        collected: formatCollectedAt(detail.collected_at, now, this.timeZone),
        relevance: formatRelevance(detail.relevance),
        alert: articleReasonAlert(detail.status, detail.status_reason, detail.threshold),
      },
    ];
  });

  protected readonly regionOptions = computed<SelectOption[]>(() =>
    this.store.regions().map((region) => ({ value: region.slug, label: region.name })),
  );

  protected readonly categoryOptions: SelectOption[] = ARTICLE_CATEGORIES.map((slug) => ({
    value: slug,
    label: categoryName(slug),
  }));

  /** The no-results words, when a search or a filter matched nothing (L2-050 AC4). */
  protected readonly noResults = computed(() => {
    const page = this.store.page();
    const query = this.query();
    const narrowed = !!(searchTerm(query.q) || query.region || query.category);
    if (!page || page.total > 0 || !narrowed) return null;
    const counts = this.store.counts();
    const total = counts ? counts[query.status ?? 'all'] : 0;
    return noResultsWords(searchTerm(query.q) ?? '', total);
  });

  protected readonly tabId = computed(() => TAB_IDS[this.query().status ?? 'all']);

  protected readonly tabs = computed<TabItem[]>(() => {
    const counts = this.store.counts();
    const count = (value: number | undefined) => (value === undefined ? '…' : formatCount(value));
    return [
      { id: 'all', label: 'All', count: count(counts?.all) },
      {
        id: 'review',
        label: 'Needs review',
        count: count(counts?.needs_review),
        countTone: 'warning',
      },
      { id: 'published', label: 'Published', count: count(counts?.published) },
      { id: 'hidden', label: 'Hidden', count: count(counts?.hidden) },
    ];
  });

  protected readonly rows = computed(() => {
    const page = this.store.page();
    if (!page) return [];
    const regions = new Map(this.store.regions().map((region) => [region.slug, region.name]));
    const now = new Date();
    return page.items.map((item) => {
      const region = regions.get(item.region) ?? item.region;
      const category = categoryName(item.category);
      return {
        item,
        href: this.router.serializeUrl(
          this.router.createUrlTree([], {
            relativeTo: this.route,
            queryParams: { article: item.id },
            queryParamsHandling: 'merge',
          }),
        ),
        title: titleLabel(item.title),
        sub: `${organizationLabel(item.organization)} · ${formatRelativeAge(item.collected_at, now)}`,
        category,
        region,
        place: `${region} · ${category}`,
        relevance: formatRelevance(item.relevance),
        status: articleStatusLabel(item.status),
        tone: articleStatusTone(item.status),
      };
    });
  });

  protected readonly range = computed(() => {
    const page = this.store.page();
    return page ? pageRange(page.page, page.page_size, page.total) : '';
  });

  protected readonly hasNext = computed(() => {
    const page = this.store.page();
    return !!page && page.page * page.page_size < page.total;
  });

  protected readonly rowKey = (row: unknown) => (row as { item: ArticleRow }).item.id;

  /** Ten rows of skeletons, the size of a page, so the list does not shift when it arrives. */
  protected readonly skeletonRows = Array.from({ length: 10 }, (_, i) => ({ key: `s${i}` }));
  protected readonly skeletonKey = (row: unknown) => (row as { key: string }).key;

  /** Nothing has ever been collected: the empty state stands alone (L2-055 AC2). */
  protected readonly nothingCollected = computed(
    () => this.store.status() === 'ready' && this.store.counts()?.all === 0,
  );

  /**
   * "Export CSV" waits while the list loads, when it did not load, when nothing has been
   * collected, and while an export is in flight (L2-054 AC5).
   */
  protected readonly exportDisabled = computed(
    () => this.store.status() !== 'ready' || this.nothingCollected() || this.store.exporting(),
  );

  /**
   * Says the export started, with the rows the list counts, then saves the file (L2-054 AC1). A
   * refusal for size asks staff to narrow the filters (AC3); any other failure offers "Try again".
   */
  protected async exportCsv(): Promise<void> {
    const name = exportFileName(new Date());
    this.notifier.notify(exportStartedNotice(name, this.store.page()?.total ?? 0));
    let file: Blob | null;
    try {
      file = await this.store.exportCsv(this.query());
    } catch (error) {
      this.notifier.notify(
        error instanceof ApiProblem && error.code === 'export_too_large'
          ? exportTooLargeNotice()
          : {
              ...exportFailedNotice(),
              action: { label: 'Try again', run: () => void this.exportCsv() },
            },
      );
      return;
    }
    if (file) saveFile(this.document, file, name);
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  protected retry(): void {
    void this.reload();
  }

  constructor() {
    const subscription = this.route.queryParamMap.subscribe((params) => {
      const values: ArticleParams = {};
      for (const key of PARAMS) values[key] = params.get(key) ?? undefined;
      this.query.set(articleQueryFromParams(values));
      void this.reload();
      void this.showArticle(this.query().article);
    });
    const resized = () => {
      this.narrow.set(this.media.matches);
      void this.syncDialog();
    };
    this.media.addEventListener('change', resized);
    inject(DestroyRef).onDestroy(() => {
      subscription.unsubscribe();
      this.media.removeEventListener('change', resized);
      this.closeDialog();
    });
  }

  protected chooseTab(id: string): void {
    const status = (Object.keys(TAB_IDS) as (keyof typeof TAB_IDS)[]).find(
      (key) => TAB_IDS[key] === id,
    );
    const chosen = !status || status === 'all' ? null : status;
    if (chosen !== this.query().status) this.change({ status: chosen });
  }

  /** The field changed: search once typing pauses, unless the term to send is the same. */
  protected typed(text: string): void {
    this.store.afterTypingPauses(() => {
      const term = searchTerm(text) ?? '';
      if (term !== (searchTerm(this.query().q) ?? '')) this.change({ q: term });
    });
  }

  protected filter(change: { region?: string; category?: string }): void {
    this.change(
      'region' in change
        ? { region: change.region || null }
        : { category: change.category || null },
    );
  }

  /** Clears the search and the filters, keeping the tab, and returns focus to the field. */
  protected clear(search: Search): void {
    this.change({ q: '', region: null, category: null });
    search.focus();
  }

  /** A plain click or Enter on a title opens its article; any other click is the browser's. */
  protected open(event: MouseEvent, id: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.focusPanel.set(true);
    this.change({ article: id });
  }

  /**
   * Closes the panel and returns focus to the title that opened it (L2-051 AC3). The URL's
   * change closes the dialog, if the panel is in one.
   */
  protected closePanel(): void {
    const id = this.query().article;
    this.focusPanel.set(false);
    this.host.nativeElement
      .querySelector<HTMLElement>(`a[data-article="${CSS.escape(id ?? '')}"]`)
      ?.focus();
    this.change({ article: null });
  }

  /**
   * Below 1024 px shows the open article in a modal dialog, and from 1024 px closes it so the
   * panel sits beside the list. The CDK dialog and its code load when first wanted.
   */
  private async syncDialog(): Promise<void> {
    const view = this.panel()[0];
    if (!this.narrow() || !view) {
      this.closeDialog();
      return;
    }
    if (this.dialog?.id === view.id) return;
    this.closeDialog();
    const { Dialog } = await import('@angular/cdk/dialog');
    const current = this.panel()[0];
    if (!this.narrow() || current?.id !== view.id || this.dialog) return;
    const ref = this.injector.get(Dialog).open<unknown, unknown, ArticlePanel>(ArticlePanel, {
      injector: this.injector,
      ariaModal: true,
      ariaLabelledBy: 'detail-title',
      autoFocus: '.detail-panel__title',
      restoreFocus: false,
      disableClose: true,
      // The dialog is the screen: the panel covers it, and the dialog's box is the panel's.
      width: '100%',
      height: '100%',
      backdropClass: 'kx-backdrop',
    });
    ref.componentInstance?.closed.subscribe(() => this.closePanel());
    ref.componentInstance?.publish.subscribe(() => void this.decide('publish'));
    ref.componentInstance?.hide.subscribe(() => void this.decide('hide'));
    this.dialog = { ref, id: view.id };
    this.pushDialog();
  }

  /**
   * Publishes or hides the open article and reports it with Undo (L2-052 AC1, AC3). The store
   * ignores a second press while one is in flight (AC5); a request that fails changes nothing and
   * offers to try again (AC4).
   */
  protected async decide(kind: 'publish' | 'hide'): Promise<void> {
    const shown = this.panel()[0];
    if (!shown) return;
    const pending = this.store.decide(kind);
    this.pushDialog();
    try {
      const decision = await pending;
      if (!decision) return;
      this.notifier.notify({
        ...decidedNotice(kind, shown.title),
        action: { label: 'Undo', run: () => void this.undo(decision, shown.title) },
      });
    } catch {
      this.notifier.notify({
        ...decisionFailedNotice(kind, shown.detail.status),
        action: { label: 'Try again', run: () => void this.decide(kind) },
      });
    } finally {
      this.pushDialog();
    }
  }

  /**
   * Returns the article to the status the decision replaced (L2-052 AC2). An Undo that fails keeps
   * the decision and offers to try again, unless the server says it can no longer be undone.
   */
  private async undo(decision: ArticleDecision, title: string): Promise<void> {
    try {
      await this.store.undo(decision);
    } catch (error) {
      this.notifier.notify(
        error instanceof ApiProblem && error.code === 'revert_unavailable'
          ? {
              tone: 'danger',
              title: 'Keryx couldn’t undo the decision',
              text: 'This decision can no longer be undone here. Publish or hide the article again instead.',
            }
          : {
              ...decisionFailedNotice('undo', decision.status),
              action: { label: 'Try again', run: () => void this.undo(decision, title) },
            },
      );
    } finally {
      this.pushDialog();
    }
  }

  /** The dialog's panel takes its inputs from here, as the inline panel does from the template. */
  private pushDialog(): void {
    const ref = this.dialog?.ref.componentRef;
    const view = this.panel()[0];
    if (!ref || view?.id !== this.dialog?.id) return;
    ref.setInput('shown', view);
    ref.setInput('busy', this.store.decisionInFlight());
  }

  /** Closes the dialog by its ref, before any navigation, without changing the URL. */
  private closeDialog(): void {
    const dialog = this.dialog;
    this.dialog = null;
    dialog?.ref.close();
  }

  /** Opens the URL's article; one that is missing or unreadable leaves the list and says so. */
  private async showArticle(id: string | null): Promise<void> {
    const result = await this.store.openArticle(id);
    if (result === 'shown' || result === 'closed') {
      void this.syncDialog();
      return;
    }
    this.notifier.notify(
      result === 'missing'
        ? {
            tone: 'warning',
            title: 'Article not found',
            text: 'The link names an article Keryx doesn’t have, so the list is shown without it.',
          }
        : {
            tone: 'danger',
            title: 'Article didn’t load',
            text: 'Keryx couldn’t reach its API just now. Open the article again in a moment.',
          },
    );
    if (this.query().article === id) this.change({ article: null }, true);
  }

  protected step(direction: -1 | 1, table: Table): void {
    this.focusAfterLoad = table;
    this.change({ page: this.query().page + direction });
  }

  /**
   * Shows the change at once and puts it in the URL; the list loads when the URL has it. Keys
   * pressed faster than a navigation completes each build on the last change, not the URL.
   */
  private change(change: Partial<ArticleQuery>, replaceUrl = false): void {
    const next = changeArticleQuery(this.query(), change);
    this.query.set(next);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: articleQueryToParams(next),
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /**
   * Loads the list the URL names. When a later page fails, the rows shown stay and a
   * notification offers to try again (L2-055 AC4); a first load that fails shows the error state.
   */
  private async reload(): Promise<void> {
    const shown = await this.store.load(this.query());
    if (!shown && this.store.status() === 'error' && this.store.page()) {
      this.notifier.notify({
        tone: 'danger',
        title: "Articles didn't load",
        text: "Keryx couldn't reach its API just now. The articles shown are from before.",
        action: { label: 'Try again', run: () => this.retry() },
      });
    }
    const table = this.focusAfterLoad;
    this.focusAfterLoad = null;
    if (shown) table?.focusCaption();
  }
}
