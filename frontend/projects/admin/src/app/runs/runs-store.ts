import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { RUNS_API, type RunDetail, type RunPage } from '@kx/api';

/** Runs shows the 14 most recent collection runs (Decision 10 of L2.md). */
export const HISTORY_RUNS = 14;

/**
 * Runs' state, held only while the screen is shown: the page of runs, the run the URL names,
 * and the chosen run's detail. Provided on the screen, so its requests end with the visit
 * (L2-089).
 */
@Injectable()
export class RunsStore {
  private readonly api = inject(RUNS_API);
  private readonly controller = new AbortController();
  /** The run the URL names, or null for the newest. */
  private requested: string | null = null;
  /** Counts detail reads, so only the latest one's answer is shown. */
  private reads = 0;

  /** The runs once read, newest first. */
  readonly page = signal<RunPage | null>(null);
  /** The chosen run: the one the URL names, else the newest once the list is read. */
  readonly selected = signal<string | null>(null);
  readonly detail = signal<RunDetail | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller.abort());
  }

  async load(): Promise<void> {
    const page = await this.api.list({ page: 1, pageSize: HISTORY_RUNS }, this.controller.signal);
    if (this.controller.signal.aborted) return;
    this.page.set(page);
    if (!this.requested) this.choose(page.items[0]?.id ?? null);
  }

  /** The URL names a run, or none: that run is chosen, or the newest once the list is read. */
  request(id: string | null): void {
    this.requested = id;
    this.choose(id ?? this.page()?.items[0]?.id ?? null);
  }

  private choose(id: string | null): void {
    if (!id || id === this.selected()) return;
    this.selected.set(id);
    void this.readDetail(id);
  }

  private async readDetail(id: string): Promise<void> {
    const read = ++this.reads;
    const detail = await this.api.get(id, this.controller.signal);
    if (read === this.reads && !this.controller.signal.aborted) this.detail.set(detail);
  }
}
