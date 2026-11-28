import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { RUNS_API, type RunDetail } from '@kx/api';
import type { FailedSource } from '@kx/domain';

/** A run in progress is read again no sooner than this after its last answer (L2-058 AC2). */
export const FOLLOW_MS = 3000;

/** The run in progress, as the client knows it. */
export interface ActiveRun {
  id: string;
  startedAt: string;
}

/**
 * Follows the collection run in progress on any screen (Page states, dialogs and notifications
 * design) and tells the screens when it has ended. It is the only reader of that run's progress:
 * Runs shows the reads it hands out rather than reading the run itself, so the run is read by
 * one poll at a time, no more often than every 3 seconds (L2-058 AC2). It makes no request while
 * no run is in progress. Provided on the shell, so it lasts the signed-in visit and its timer
 * ends with it.
 */
@Injectable()
export class CollectionWatcher {
  private readonly runs = inject(RUNS_API);
  private readonly controller = new AbortController();
  private readonly ends = new Set<(run: RunDetail) => void>();
  private readonly reads = new Set<(run: RunDetail) => void>();
  private timer: ReturnType<typeof setTimeout> | undefined;
  /** Runs whose failed sources are being fetched again, with those sources (L2-059). */
  private readonly retries = new Map<string, readonly FailedSource[]>();

  /** The run in progress, or null when none is known to be. */
  readonly active = signal<ActiveRun | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller.abort();
      clearTimeout(this.timer);
    });
  }

  /**
   * What a screen read about the run in progress: the newest run while it runs, or null when the
   * newest has ended or the status serves none.
   */
  track(run: { id: string; started_at: string } | null): void {
    clearTimeout(this.timer);
    this.active.set(run ? { id: run.id, startedAt: run.started_at } : null);
    if (run) this.follow();
  }

  /** A run's detail was read: the run followed goes on, or has ended. */
  observed(run: RunDetail): void {
    if (run.id !== this.active()?.id) return;
    clearTimeout(this.timer);
    if (run.outcome === 'running') {
      this.follow();
      return;
    }
    this.active.set(null);
    for (const ended of this.ends) ended(run);
  }

  /** The run is a retry of `failing`: its end says which of them recovered (L2-059 AC2, AC3). */
  expectRetry(runId: string, failing: readonly FailedSource[]): void {
    this.retries.set(runId, failing);
  }

  /** The sources a run's retry set out to fetch again, once, or null when it was no retry. */
  takeRetry(runId: string): readonly FailedSource[] | null {
    const failing = this.retries.get(runId) ?? null;
    this.retries.delete(runId);
    return failing;
  }

  /** Calls `ended` with the final run each time the run in progress ends, until `destroyRef`. */
  onEnd(ended: (run: RunDetail) => void, destroyRef: DestroyRef): void {
    this.ends.add(ended);
    destroyRef.onDestroy(() => this.ends.delete(ended));
  }

  /** Calls `read` with each read of the run in progress, until `destroyRef`. */
  onRead(read: (run: RunDetail) => void, destroyRef: DestroyRef): void {
    this.reads.add(read);
    destroyRef.onDestroy(() => this.reads.delete(read));
  }

  private follow(): void {
    this.timer = setTimeout(() => void this.read(), FOLLOW_MS);
  }

  private async read(): Promise<void> {
    const active = this.active();
    if (!active) return;
    try {
      const run = await this.runs.get(active.id, this.controller.signal);
      for (const read of this.reads) read(run);
      this.observed(run);
    } catch {
      // A read that failed is tried again after the same wait, while the run is still followed.
      if (!this.controller.signal.aborted && this.active()?.id === active.id) this.follow();
    }
  }
}
