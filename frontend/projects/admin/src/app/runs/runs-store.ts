import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { RUNS_API, type RunDetail, type RunPage } from '@kx/api';

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
  private readonly controller = new AbortController();
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

  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller.abort());
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
      void this.load();
    }
    this.choose(id ?? this.page()?.items[0]?.id ?? null);
  }

  private async load(): Promise<void> {
    const read = ++this.listReads;
    const page = await this.api.list(
      { page: 1, pageSize: HISTORY_RUNS, problems: this.problems() ?? false },
      this.controller.signal,
    );
    if (read !== this.listReads || this.controller.signal.aborted) return;
    this.page.set(page);
    if (!this.requested) this.choose(page.items[0]?.id ?? null);
  }

  private choose(id: string | null): void {
    if (!id || id === this.selected()) return;
    this.selected.set(id);
    void this.readDetail(id);
  }

  private async readDetail(id: string): Promise<void> {
    const read = ++this.detailReads;
    const detail = await this.api.get(id, this.controller.signal);
    if (read === this.detailReads && !this.controller.signal.aborted) this.detail.set(detail);
  }
}
