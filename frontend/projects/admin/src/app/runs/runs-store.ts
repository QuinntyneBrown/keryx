import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ApiProblem, NOTIFIER, RUNS_API, type RunDetail, type RunPage } from '@kx/api';
import {
  canRetryFailedSources,
  failedSources,
  retryStartedNotice,
  skeletonDelay,
} from '@kx/domain';
import { CollectionWatcher } from '../schedule/collection-watcher';

/** A failed read's reference, the response's trace id, for staff to quote. */
function referenceOf(error: unknown): string | null {
  return error instanceof ApiProblem ? (error.reference ?? null) : null;
}

/** Runs shows the 14 most recent collection runs (Decision 10 of L2.md). */
export const HISTORY_RUNS = 14;

/**
 * Runs' state, held only while the screen is shown: the page of runs, all of them or those with
 * problems, the run the URL names, and the chosen run's detail. Provided on the screen, so its
 * requests end with the visit (L2-089).
 */
@Injectable()
export class RunsStore {
  private readonly api = inject(RUNS_API);
  private readonly watcher = inject(CollectionWatcher);
  private readonly notifier = inject(NOTIFIER);
  private readonly controller = new AbortController();
  private readonly skeleton = skeletonDelay();
  /** The run the URL names, or null for the newest. */
  private requested: string | null = null;
  /** Counts list and detail reads, so only the latest one's answer is shown. */
  private listReads = 0;
  private detailReads = 0;

  /** Only runs with problems are listed (`?problems=true`). */
  readonly problems = signal<boolean | null>(null);
  /** The runs once read, newest first. */
  readonly page = signal<RunPage | null>(null);
  /** The chosen run: the one the URL names, else the newest once the list is read. */
  readonly selected = signal<string | null>(null);
  readonly detail = signal<RunDetail | null>(null);
  /** The list failed: the page error stands in, with this reference (L2-061 AC3). */
  readonly listFailed = signal<{ reference: string | null } | null>(null);
  /** The chosen run's detail failed; the list stays usable (L2-061 AC4). */
  readonly detailFailed = signal<{ reference: string | null } | null>(null);
  /** Try again is reading the list, or the detail, again. */
  readonly retrying = signal(false);
  readonly retryingDetail = signal(false);
  /** The list is being read for the first time, or for a new filter. */
  readonly listLoading = computed(() => !this.page() && !this.listFailed());
  /** No list has been read yet: the filter waits for the first (as the loading mock shows). */
  readonly opening = signal(true);
  /** The chosen run's detail is being read. */
  readonly detailLoading = computed(
    () => !!this.selected() && !this.detail() && !this.detailFailed(),
  );
  /** A read has taken over 300 ms: skeletons stand in for what is loading (L2-061 AC1). */
  readonly slow = signal(false);
  /** "Retry failed sources" is waiting for the API's answer. */
  readonly retryingSources = signal(false);
  /** The chosen run offers "Retry failed sources" (L2-059 AC4). */
  readonly canRetry = computed(() => {
    const run = this.detail();
    return !!run && canRetryFailedSources(run);
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => {
      this.controller.abort();
      this.skeleton.cancel();
    });
    // The chosen run in progress advances as the watcher reads it (L2-058 AC1).
    this.watcher.onRead((run) => {
      if (run.id === this.selected()) this.detail.set(run);
    }, destroyRef);
  }

  /**
   * The URL names a run, or none, and whether only runs with problems are listed. A new filter
   * reads the list again; the run named is chosen, or the newest once the list is read.
   */
  request(id: string | null, problems: boolean): void {
    this.requested = id;
    if (problems !== this.problems()) {
      this.problems.set(problems);
      this.page.set(null);
      this.selected.set(null);
      this.detail.set(null);
      this.detailFailed.set(null);
      void this.load();
    }
    this.choose(id ?? this.page()?.items[0]?.id ?? null);
    this.settle();
  }

  /** Reads the list again, as when a run has just started, keeping the run chosen. */
  refresh(): void {
    void this.load();
  }

  /**
   * Fetches the chosen run's failed sources again (L2-059 AC1), sending one request while one is
   * out: says which, then hands the run it runs as to `shown` while the watcher follows it. A run
   * that failed starts a new run instead (L2-008 AC5). Another run in progress is said, and
   * followed (AC5); any other refusal says the retry did not start, with "Try again".
   */
  async retryFailedSources(shown: (runId: string) => unknown): Promise<void> {
    const run = this.detail();
    if (!run || this.retryingSources()) return;
    this.retryingSources.set(true);
    try {
      const { id } = await this.api.retry(run.id, this.controller.signal);
      const failing = failedSources(run.issues);
      if (id === run.id) this.watcher.expectRetry(id, failing);
      this.notifier.notify(
        id === run.id
          ? {
              tone: 'info',
              ...retryStartedNotice(
                run.sources_failed,
                failing.map((s) => s.name),
              ),
            }
          : {
              tone: 'info',
              title: 'Collection started',
              text: 'The run failed, so a new run checks every source.',
            },
      );
      // The list read that follows finds the run in progress, for the watcher to follow.
      shown(id);
    } catch (error) {
      if (this.controller.signal.aborted) return;
      if (error instanceof ApiProblem && error.code === 'run_in_progress') {
        this.notifier.notify({
          tone: 'info',
          title: 'Collection already running',
          text: 'Retry failed sources once it has finished.',
        });
        if (error.runId)
          this.watcher.track({ id: error.runId, started_at: new Date().toISOString() });
        return;
      }
      const reference = referenceOf(error);
      this.notifier.notify({
        tone: 'danger',
        title: 'Retry didn’t start',
        text: `Keryx couldn’t reach its API just now, so no source was fetched again.${reference ? ` Reference ${reference}` : ''}`,
        action: { label: 'Try again', run: () => void this.retryFailedSources(shown) },
      });
    } finally {
      this.retryingSources.set(false);
    }
  }

  /** Reads the list and the chosen run again, as when its failed sources are being retried. */
  reload(): void {
    void this.load();
    const id = this.selected();
    if (id) void this.readDetail(id);
  }

  /** Reads the list again after the page error, busy meanwhile (L2-073 AC3). */
  async retry(): Promise<void> {
    this.retrying.set(true);
    try {
      await this.load();
    } finally {
      this.retrying.set(false);
    }
  }

  /** Reads the chosen run again after its detail failed. */
  async retryDetail(): Promise<void> {
    const id = this.selected();
    if (!id) return;
    this.retryingDetail.set(true);
    try {
      await this.readDetail(id);
    } finally {
      this.retryingDetail.set(false);
    }
  }

  /**
   * Skeletons wait 300 ms while something loads, and go once nothing does. Called when a read
   * starts or ends; never once the visit is over, so no timer outlives it.
   */
  private settle(): void {
    if (this.controller.signal.aborted) return;
    if (this.listLoading() || this.detailLoading()) {
      this.skeleton.run(() => this.slow.set(true));
      return;
    }
    this.skeleton.cancel();
    this.slow.set(false);
  }

  private async load(): Promise<void> {
    const read = ++this.listReads;
    const current = () => read === this.listReads && !this.controller.signal.aborted;
    try {
      const page = await this.api.list(
        { page: 1, pageSize: HISTORY_RUNS, problems: this.problems() ?? false },
        this.controller.signal,
      );
      if (!current()) return;
      this.listFailed.set(null);
      this.opening.set(false);
      this.page.set(page);
      // The full history says whether a run is in progress, the newest or one retried; a
      // filtered list cannot.
      if (!this.problems()) {
        this.watcher.track(page.items.find((item) => item.outcome === 'running') ?? null);
      }
      if (!this.requested) this.choose(page.items[0]?.id ?? null);
    } catch (error) {
      if (current()) this.listFailed.set({ reference: referenceOf(error) });
    } finally {
      this.settle();
    }
  }

  private choose(id: string | null): void {
    if (!id || id === this.selected()) return;
    this.selected.set(id);
    this.detail.set(null);
    this.detailFailed.set(null);
    void this.readDetail(id);
  }

  private async readDetail(id: string): Promise<void> {
    const read = ++this.detailReads;
    const current = () => read === this.detailReads && !this.controller.signal.aborted;
    try {
      const detail = await this.api.get(id, this.controller.signal);
      if (!current()) return;
      this.detail.set(detail);
      this.watcher.observed(detail);
    } catch (error) {
      if (!current()) return;
      this.detail.set(null);
      this.detailFailed.set({ reference: referenceOf(error) });
    } finally {
      this.settle();
    }
  }
}
