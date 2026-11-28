import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ApiProblem, NOTIFIER, OVERVIEW_API, RUNS_API, type ShellSchedule } from '@kx/api';
import { collectionStartedText } from '@kx/domain';
import { CollectionWatcher } from './collection-watcher';

/** How often the countdown to the next run is worked out again. */
const CLOCK_MS = 30_000;

/**
 * The shell's schedule (Admin shell design): the next collection and the failing sources, read
 * once for the signed-in visit and shared by the screens that show it, with the time the
 * countdown reads, and "Run now" (Run collection now design). Provided on the shell, beside the
 * notifier it reports through, so it lasts the signed-in visit (ADR 0005).
 */
@Injectable()
export class ScheduleStore {
  private readonly api = inject(OVERVIEW_API);
  private readonly runs = inject(RUNS_API);
  private readonly notifier = inject(NOTIFIER);
  private readonly watcher = inject(CollectionWatcher);
  private readonly controller = new AbortController();
  private requested = false;

  /** The schedule once read; null before then or when it failed. */
  readonly schedule = signal<ShellSchedule | null>(null);
  readonly now = signal(new Date());
  /** "Run now" is waiting for the API's answer. */
  readonly starting = signal(false);
  /** The collection run in progress, as the watcher knows it. */
  readonly active = this.watcher.active;
  /** "Run now" reads a disabled, busy "Collecting…" (L2-058 AC4). */
  readonly collecting = computed(() => this.starting() || this.active() !== null);

  constructor() {
    const clock = setInterval(() => this.now.set(new Date()), CLOCK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
  }

  /**
   * Starts a collection run (L2-047 AC1): says so with how many sources it checks, and hands the
   * new run's id to `shown` for the screen to show it. A run already in progress is no error: its
   * id is handed over, unannounced (AC2). A start that fails says so, with "Try again", which
   * starts it again for the same screen (AC3). A press while a start is out sends nothing (AC4).
   */
  async runNow(shown: (runId: string) => unknown): Promise<void> {
    if (this.collecting()) return;
    this.starting.set(true);
    let runId: string | null;
    try {
      const started = await this.runs.start(this.controller.signal);
      this.notifier.notify({
        tone: 'info',
        title: 'Collection started',
        text: collectionStartedText(started.sources_total, started.estimated_duration_ms),
      });
      runId = started.id;
    } catch (error) {
      if (error instanceof ApiProblem && error.code === 'run_in_progress') {
        runId = error.runId ?? null;
      } else {
        const reference = error instanceof ApiProblem ? error.reference : undefined;
        this.notifier.notify({
          tone: 'danger',
          title: 'Collection didn’t start',
          text: `Keryx couldn’t reach its API just now, so no run started.${reference ? ` Reference ${reference}` : ''}`,
          action: { label: 'Try again', run: () => void this.runNow(shown) },
        });
        return;
      }
    } finally {
      this.starting.set(false);
    }
    if (runId) shown(runId);
  }

  /** Reads the schedule the first time a screen asks for it. */
  load(): void {
    if (this.requested) return;
    this.requested = true;
    this.api.schedule(this.controller.signal).then(
      (schedule) => {
        this.schedule.set(schedule);
        this.now.set(new Date());
      },
      // Without it the header leaves the schedule out; the screen itself still works.
      () => undefined,
    );
  }
}
