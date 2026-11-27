import {
  computed,
  DestroyRef,
  inject,
  Injectable,
  signal,
  type WritableSignal,
} from '@angular/core';
import {
  ApiProblem,
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

/** One part of Overview, loaded on its own: its data once read, or the reference of its failure. */
export type Section<T> =
  { state: 'loading' } | { state: 'ready'; data: T } | { state: 'error'; reference: string | null };

/**
 * Overview's state, held only while the screen is shown: four sections read in parallel, each on
 * its own, so one failing leaves the others shown (L2-045 AC5), and the time the countdown reads.
 * Provided on the screen, so its requests and timer end with the visit (L2-089).
 */
@Injectable()
export class OverviewStore {
  private readonly api = inject(OVERVIEW_API);
  private readonly runs = inject(RUNS_API);
  private readonly articles = inject(ARTICLES_API);
  private readonly controller = new AbortController();

  readonly status = signal<Section<OverviewStatus>>({ state: 'loading' });
  /** The last runs, newest first as served. */
  readonly chart = signal<Section<readonly RunSummary[]>>({ state: 'loading' });
  /** The newest articles, with the regions that name them. */
  readonly latest = signal<Section<{ page: ArticlePage; regions: readonly Region[] }>>({
    state: 'loading',
  });
  readonly issues = signal<Section<OpenIssuePage>>({ state: 'loading' });
  readonly now = signal(new Date());
  /** Try again is repeating every request. */
  readonly retrying = signal(false);

  private readonly sections = [this.status, this.chart, this.latest, this.issues] as const;

  /** Every request failed: the page error stands in for the screen (L2-046 AC3). */
  readonly allFailed = computed(() =>
    this.sections.every((section) => section().state === 'error'),
  );

  /** The reference of the first failure, for the page error. */
  readonly reference = computed(() => {
    for (const section of this.sections) {
      const shown = section();
      if (shown.state === 'error' && shown.reference) return shown.reference;
    }
    return null;
  });

  /** No source and no run exist: the empty state stands in for the screen (L2-046 AC2). */
  readonly isEmpty = computed(() => {
    const status = this.status();
    return status.state === 'ready' && !status.data.has_sources && !status.data.has_runs;
  });

  constructor() {
    const clock = setInterval(() => this.now.set(new Date()), CLOCK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
  }

  /**
   * Reads every section, each shown as it answers; resolves once all have. With `together`, the
   * answers are shown at once when the last arrives, so a retry's page error stays until then.
   */
  async load(together = false): Promise<void> {
    const { signal: abort } = this.controller;
    const held: (() => void)[] = [];
    const settle = async <T>(section: WritableSignal<Section<T>>, fetch: () => Promise<T>) => {
      const next = await this.read(fetch);
      const show = () => {
        if (!abort.aborted) section.set(next);
      };
      if (together) held.push(show);
      else show();
    };
    await Promise.all([
      settle(this.status, () => this.api.status(abort)),
      settle(this.chart, async () => {
        const page = await this.runs.list({ page: 1, pageSize: CHART_RUNS }, abort);
        return page.items;
      }),
      settle(this.latest, async () => {
        const [page, regions] = await Promise.all([
          this.articles.list({ page: 1, pageSize: LATEST_ARTICLES }, abort),
          this.articles.regions(abort),
        ]);
        return { page, regions };
      }),
      settle(this.issues, () => this.runs.openIssues(ATTENTION_ISSUES, abort)),
    ]);
    for (const show of held) show();
    if (!abort.aborted) this.now.set(new Date());
  }

  /** Repeats every request after the page error, busy meanwhile (L2-073 AC3). */
  async retry(): Promise<void> {
    this.retrying.set(true);
    try {
      await this.load(true);
    } finally {
      this.retrying.set(false);
    }
  }

  /** A section's next state: its data, or its failure with the response's trace id. */
  private async read<T>(fetch: () => Promise<T>): Promise<Section<T>> {
    try {
      return { state: 'ready', data: await fetch() };
    } catch (error) {
      return {
        state: 'error',
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      };
    }
  }
}
