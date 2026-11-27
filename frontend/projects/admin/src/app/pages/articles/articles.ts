import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ARTICLES_API, type ArticleRow, type ArticleStatus } from '@kx/api';
import {
  Badge,
  Meter,
  PageBody,
  PageHeader,
  Pagination,
  Table,
  Tabs,
  type TabItem,
  type TableColumn,
} from '@kx/components';
import {
  articleQueryFromParams,
  articleQueryToParams,
  articleStatusLabel,
  articleStatusTone,
  categoryName,
  changeArticleQuery,
  formatCount,
  formatRelativeAge,
  formatRelevance,
  organizationLabel,
  pageRange,
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
  imports: [Badge, Meter, PageBody, PageHeader, Pagination, Table, Tabs],
  providers: [{ provide: ARTICLES_API, useClass: HttpArticlesApi }, ArticlesStore],
  templateUrl: './articles.html',
  styleUrl: './articles.scss',
})
export default class Articles {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
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

  protected step(direction: -1 | 1, table: Table): void {
    this.focusAfterLoad = table;
    this.change({ page: this.query().page + direction });
  }

  /**
   * Shows the change at once and puts it in the URL; the list loads when the URL has it. Keys
   * pressed faster than a navigation completes each build on the last change, not the URL.
   */
  private change(change: Partial<ArticleQuery>): void {
    const next = changeArticleQuery(this.query(), change);
    this.query.set(next);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: articleQueryToParams(next),
      queryParamsHandling: 'merge',
    });
  }

  private async reload(): Promise<void> {
    const shown = await this.store.load(this.query());
    const table = this.focusAfterLoad;
    this.focusAfterLoad = null;
    if (shown) table?.focusCaption();
  }
}
