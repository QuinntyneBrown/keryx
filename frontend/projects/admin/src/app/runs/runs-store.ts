import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { RUNS_API, type RunPage } from '@kx/api';

/** Runs shows the 14 most recent collection runs (Decision 10 of L2.md). */
export const HISTORY_RUNS = 14;

/**
 * Runs' state, held only while the screen is shown: the page of runs. Provided on the screen,
 * so its requests end with the visit (L2-089).
 */
@Injectable()
export class RunsStore {
  private readonly api = inject(RUNS_API);
  private readonly controller = new AbortController();

  /** The runs once read, newest first. */
  readonly page = signal<RunPage | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller.abort());
  }

  async load(): Promise<void> {
    const page = await this.api.list({ page: 1, pageSize: HISTORY_RUNS }, this.controller.signal);
    if (!this.controller.signal.aborted) this.page.set(page);
  }
}
