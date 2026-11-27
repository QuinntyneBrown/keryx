import { Component, computed, DestroyRef, ElementRef, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ARTICLES_API, NOTIFIER, type ArticleRow, type ArticleStatus } from '@kx/api';
import {
  Alert,
  Badge,
  Btn,
  DetailPanel,
  Dl,
  Empty,
  Icon,
  Meter,
  PageBody,
  PageHeader,
  Pagination,
  Search,
  Select,
  Table,
  Tabs,
  Toolbar,
  type DlItem,
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
} from '@kx/domain';
import { ArticlesStore } from '../../articles/articles-store';
import { HttpArticlesApi } from '../../articles/http-articles-api';

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
    Alert,
    Badge,
    Btn,
    DetailPanel,
    Dl,
    Empty,
    Icon,
    Meter,
    PageBody,
    PageHeader,
    Pagination,
    Search,
    Select,
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

  protected readonly facts: DlItem[] = [
    { key: 'organization', term: 'Organisation' },
    { key: 'source', term: 'Source' },
    { key: 'category', term: 'Category' },
    { key: 'region', term: 'Region' },
    { key: 'published', term: 'Published' },
    { key: 'collected', term: 'Collected' },
    { key: 'relevance', term: 'Relevance' },
    { key: 'tags', term: 'Tags' },
  ];

  /**
   * The open article as the panel shows it, in a list of at most one, so a different article
   * draws a new panel, which takes focus when a person opened it.
   */
  protected readonly panel = computed(() => {
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
    const count = (value: number | undefined) =>
      value === undefined ? undefined : formatCount(value);
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

  constructor() {
    const subscription = this.route.queryParamMap.subscribe((params) => {
      const values: ArticleParams = {};
      for (const key of PARAMS) values[key] = params.get(key) ?? undefined;
      this.query.set(articleQueryFromParams(values));
      void this.reload();
      void this.showArticle(this.query().article);
    });
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
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

  /** Closes the panel and returns focus to the title that opened it (L2-051 AC3). */
  protected closePanel(): void {
    const id = this.query().article;
    this.focusPanel.set(false);
    this.host.nativeElement
      .querySelector<HTMLElement>(`a[data-article="${CSS.escape(id ?? '')}"]`)
      ?.focus();
    this.change({ article: null });
  }

  /** Opens the URL's article; one that is missing or unreadable leaves the list and says so. */
  private async showArticle(id: string | null): Promise<void> {
    const result = await this.store.openArticle(id);
    if (result !== 'missing' && result !== 'failed') return;
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

  private async reload(): Promise<void> {
    const shown = await this.store.load(this.query());
    const table = this.focusAfterLoad;
    this.focusAfterLoad = null;
    if (shown) table?.focusCaption();
  }
}
