import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { OVERVIEW_API, type OverviewStatus } from '@kx/api';

/** How often the countdown to the next run is worked out again. */
const CLOCK_MS = 30_000;

/**
 * Overview's state, held only while the screen is shown: its status and the time the countdown
 * reads. Provided on the screen, so its request and timer end with the visit (L2-089).
 */
@Injectable()
export class OverviewStore {
  private readonly api = inject(OVERVIEW_API);
  private readonly controller = new AbortController();

  readonly status = signal<OverviewStatus | null>(null);
  readonly now = signal(new Date());

  constructor() {
    const clock = setInterval(() => this.now.set(new Date()), CLOCK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
  }

  /** Reads the status; resolves once it is shown. */
  async load(): Promise<void> {
    const status = await this.api.status(this.controller.signal);
    if (this.controller.signal.aborted) return;
    this.status.set(status);
    this.now.set(new Date());
  }
}
