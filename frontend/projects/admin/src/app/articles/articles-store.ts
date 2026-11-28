import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  ApiProblem,
  ARTICLES_API,
  type ArticleCounts,
  type ArticleDecision,
  type ArticleDetail,
  type ArticlePage,
  type Region,
} from '@kx/api';
import { searchPause, searchTerm, skeletonDelay, type ArticleQuery } from '@kx/domain';

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
  private detailController: AbortController | null = null;
  private readonly decisions = new AbortController();
  private readonly exports = new AbortController();
  private readonly pause = searchPause();
  private readonly skeleton = skeletonDelay();

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly page = signal<ArticlePage | null>(null);
  readonly counts = signal<ArticleCounts | null>(null);
  readonly regions = signal<readonly Region[]>([]);
  readonly reference = signal<string | null>(null);
  /** The first load has had no answer for 300 ms: skeleton rows stand in (L2-055 AC1). */
  readonly slow = signal(false);
  /** The open article, or null while none is open or it is loading. */
  readonly detail = signal<ArticleDetail | null>(null);
  /** The decision being sent: while one is, another is ignored (L2-052 AC5). */
  readonly decisionInFlight = signal<'publish' | 'hide' | null>(null);
  /** An export is waiting for its file: "Export CSV" waits too. */
  readonly exporting = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.detailController?.abort();
      this.decisions.abort();
      this.exports.abort();
      this.pause.cancel();
      this.skeleton.cancel();
    });
  }

  /** Runs `search` once typing has paused; a key within the pause cancels the pending one. */
  afterTypingPauses(search: () => void): void {
    this.pause.run(search);
  }

  /**
   * Publishes or hides the open article. Resolves the decision once the article, its row and the
   * status totals show it; null when another decision is in flight or the screen has gone; and
   * rejects when the request fails, with nothing changed.
   */
  async decide(kind: 'publish' | 'hide'): Promise<ArticleDecision | null> {
    const article = this.detail();
    if (!article || this.decisionInFlight()) return null;
    this.decisionInFlight.set(kind);
    const { signal } = this.decisions;
    try {
      const decision = await this.api[kind](article.id, signal);
      if (signal.aborted) return null;
      this.apply(decision);
      await this.refreshCounts(signal);
      return decision;
    } finally {
      this.decisionInFlight.set(null);
    }
  }

  /**
   * Undoes a decision (L2-052 AC2): the article returns to the status it replaced, and is read
   * again for the reason its status carries. Resolves false when the screen has gone; rejects when
   * the request fails.
   */
  async undo(decision: ArticleDecision): Promise<boolean> {
    if (!decision.previous_status) return false;
    const { signal } = this.decisions;
    const reverted = await this.api.revert(decision.id, decision.previous_status, signal);
    if (signal.aborted) return false;
    this.apply(reverted);
    await Promise.all([this.reread(reverted.id, signal), this.refreshCounts(signal)]);
    return true;
  }

  private async reread(id: string, signal: AbortSignal): Promise<void> {
    if (this.detail()?.id !== id) return;
    try {
      const detail = await this.api.get(id, signal);
      if (!signal.aborted && this.detail()?.id === id) this.detail.set(detail);
    } catch {
      // The status shown is the server's; only the reason waits for the next open.
    }
  }

  /** Shows a decision's status on the open article and its row. */
  private apply(decision: ArticleDecision): void {
    const detail = this.detail();
    if (detail?.id === decision.id) {
      this.detail.set({ ...detail, status: decision.status, decided_by: decision.decided_by });
    }
    const page = this.page();
    if (page?.items.some((row) => row.id === decision.id)) {
      this.page.set({
        ...page,
        items: page.items.map((row) =>
          row.id === decision.id ? { ...row, status: decision.status } : row,
        ),
      });
    }
  }

  /** Reads the status totals again; the tabs keep the last ones when the read fails. */
  private async refreshCounts(signal: AbortSignal): Promise<void> {
    try {
      const counts = await this.api.counts(signal);
      if (!signal.aborted) this.counts.set(counts);
    } catch {
      // The decision stands; the totals catch up on the next load.
    }
  }

  private settle(): void {
    this.skeleton.cancel();
    this.slow.set(false);
  }

  /**
   * Opens one article, or closes it with null. Resolves `missing` when no article has the id
   * and `failed` when it could not be read; the panel then stays closed.
   */
  async openArticle(id: string | null): Promise<'shown' | 'missing' | 'failed' | 'closed'> {
    this.detailController?.abort();
    if (id === null) {
      this.detail.set(null);
      return 'closed';
    }
    if (this.detail()?.id === id) return 'shown';
    const controller = new AbortController();
    this.detailController = controller;
    try {
      const detail = await this.api.get(id, controller.signal);
      if (controller.signal.aborted) return 'closed';
      this.detail.set(detail);
      return 'shown';
    } catch (error) {
      if (controller.signal.aborted) return 'closed';
      this.detail.set(null);
      return error instanceof ApiProblem && error.code === 'article_not_found'
        ? 'missing'
        : 'failed';
    }
  }

  /**
   * The CSV file of the articles the query's tab, search and filters match (L2-054). It rejects
   * with the API's refusal, or with the abort when the screen is left first.
   */
  async exportCsv(query: ArticleQuery): Promise<Blob> {
    this.exporting.set(true);
    try {
      return await this.api.exportCsv(
        {
          status: query.status ?? undefined,
          q: searchTerm(query.q) ?? undefined,
          region: query.region ?? undefined,
          category: query.category ?? undefined,
        },
        this.exports.signal,
      );
    } finally {
      this.exporting.set(false);
    }
  }

  /** Loads the page the query names; resolves true once it is shown. */
  async load(query: ArticleQuery): Promise<boolean> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    const { signal } = controller;
    this.status.set('loading');
    if (!this.page()) this.skeleton.run(() => this.slow.set(true));
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
      this.settle();
      this.page.set(page);
      this.counts.set(counts);
      this.regions.set(regions);
      this.status.set('ready');
      return true;
    } catch (error) {
      if (signal.aborted) return false;
      this.settle();
      this.reference.set(error instanceof ApiProblem ? (error.reference ?? null) : null);
      this.status.set('error');
      return false;
    }
  }
}
