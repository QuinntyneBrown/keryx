import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  ApiProblem,
  ARTICLES_API,
  type ArticleCounts,
  type ArticlePage,
  type Region,
} from '@kx/api';
import { searchPause, searchTerm, type ArticleQuery } from '@kx/domain';

/** The page size the Articles screen asks for (L2-048). */
export const ARTICLES_PAGE_SIZE = 10;

/**
 * The Articles screen's list: one server page at a time, the status totals and the region names,
 * held only while the screen is shown. A new load cancels the one in flight, and the rows already
 * shown stay until the next page arrives. Provided on the screen, so it ends with the visit.
 */
@Injectable()
export class ArticlesStore {
  private readonly api = inject(ARTICLES_API);
  private controller: AbortController | null = null;
  private readonly pause = searchPause();

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly page = signal<ArticlePage | null>(null);
  readonly counts = signal<ArticleCounts | null>(null);
  readonly regions = signal<readonly Region[]>([]);
  readonly reference = signal<string | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.pause.cancel();
    });
  }

  /** Runs `search` once typing has paused; a key within the pause cancels the pending one. */
  afterTypingPauses(search: () => void): void {
    this.pause.run(search);
  }

  /** Loads the page the query names; resolves true once it is shown. */
  async load(query: ArticleQuery): Promise<boolean> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    const { signal } = controller;
    this.status.set('loading');
    try {
      const [page, counts, regions] = await Promise.all([
        this.api.list(
          {
            page: query.page,
            pageSize: ARTICLES_PAGE_SIZE,
            status: query.status ?? undefined,
            q: searchTerm(query.q) ?? undefined,
            region: query.region ?? undefined,
            category: query.category ?? undefined,
          },
          signal,
        ),
        this.api.counts(signal),
        this.regions().length ? this.regions() : this.api.regions(signal),
      ]);
      if (signal.aborted) return false;
      this.page.set(page);
      this.counts.set(counts);
      this.regions.set(regions);
      this.status.set('ready');
      return true;
    } catch (error) {
      if (signal.aborted) return false;
      this.reference.set(error instanceof ApiProblem ? (error.reference ?? null) : null);
      this.status.set('error');
      return false;
    }
  }
}
