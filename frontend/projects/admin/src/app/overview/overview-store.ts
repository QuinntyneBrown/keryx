import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  ARTICLES_API,
  OVERVIEW_API,
  RUNS_API,
  type ArticlePage,
  type OpenIssuePage,
  type OverviewStatus,
  type Region,
  type RunSummary,
} from '@kx/api';

/** The run chart's span: the last 14 collection runs (L2-044). */
export const CHART_RUNS = 14;

/** Latest articles: the 7 newest of any status (L2-045 AC1). */
export const LATEST_ARTICLES = 7;

/** Needs attention: the 5 most severe open issues (L2-045 AC6). */
export const ATTENTION_ISSUES = 5;

/** How often the countdown to the next run is worked out again. */
const CLOCK_MS = 30_000;

/**
 * Overview's state, held only while the screen is shown: its status and the time the countdown
 * reads. Provided on the screen, so its request and timer end with the visit (L2-089).
 */
@Injectable()
export class OverviewStore {
  private readonly api = inject(OVERVIEW_API);
  private readonly runs = inject(RUNS_API);
  private readonly articles = inject(ARTICLES_API);
  private readonly controller = new AbortController();

  readonly status = signal<OverviewStatus | null>(null);
  /** The last runs, newest first as served. */
  readonly chart = signal<readonly RunSummary[] | null>(null);
  /** The newest articles, with the regions that name them. */
  readonly latest = signal<{ page: ArticlePage; regions: readonly Region[] } | null>(null);
  readonly issues = signal<OpenIssuePage | null>(null);
  readonly now = signal(new Date());

  constructor() {
    const clock = setInterval(() => this.now.set(new Date()), CLOCK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
  }

  /** Reads each section on its own, so one never waits for another; resolves once all are shown. */
  async load(): Promise<void> {
    await Promise.all([this.loadStatus(), this.loadChart(), this.loadLatest(), this.loadIssues()]);
  }

  private async loadStatus(): Promise<void> {
    const status = await this.api.status(this.controller.signal);
    if (this.controller.signal.aborted) return;
    this.status.set(status);
    this.now.set(new Date());
  }

  private async loadChart(): Promise<void> {
    const page = await this.runs.list({ page: 1, pageSize: CHART_RUNS }, this.controller.signal);
    if (this.controller.signal.aborted) return;
    this.chart.set(page.items);
  }

  private async loadLatest(): Promise<void> {
    const { signal } = this.controller;
    const [page, regions] = await Promise.all([
      this.articles.list({ page: 1, pageSize: LATEST_ARTICLES }, signal),
      this.articles.regions(signal),
    ]);
    if (signal.aborted) return;
    this.latest.set({ page, regions });
  }

  private async loadIssues(): Promise<void> {
    const issues = await this.runs.openIssues(ATTENTION_ISSUES, this.controller.signal);
    if (this.controller.signal.aborted) return;
    this.issues.set(issues);
  }
}
