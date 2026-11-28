import { Dialog } from '@angular/cdk/dialog';
import { computed, DestroyRef, inject, Injectable, Injector, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  ApiProblem,
  NOTIFIER,
  OVERVIEW_API,
  RUNS_API,
  type RunDetail,
  type ShellSchedule,
} from '@kx/api';
import {
  collectionEndedNotice,
  collectionStartedText,
  failedSources,
  summarizeRetry,
} from '@kx/domain';
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
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);
  private readonly injector = inject(Injector);
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
    const clock = setInterval(() => this.tick(), CLOCK_MS);
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
    this.watcher.onEnd((run) => this.announce(run), destroyRef);
  }

  /**
   * Says how the run ended (L2-076 AC2–AC4), once: the watcher ends each run once. "View run"
   * closes any dialog and opens the run; "Retry now" starts another, which the watcher follows.
   */
  /** Opens "Edit source" where staff are, loading it when first wanted. */
  private async editSource(id: string, name: string): Promise<void> {
    const { editSourceById } = await import('../sources/edit-source');
    await editSourceById(this.injector, id, name);
  }

  private announce(run: RunDetail): void {
    const retried = this.watcher.takeRetry(run.id);
    if (retried) {
      // A retry says which of its sources recovered. "Edit source" opens the dialog for the one
      // still failing; "Edit sources" opens Sources, which lists failing sources first.
      const failing = failedSources(run.issues);
      const summary = summarizeRetry(retried, failing);
      const one = failing.length === 1 ? failing[0] : null;
      this.notifier.notify({
        tone: summary.tone,
        title: summary.title,
        text: summary.text,
        ...(summary.action
          ? {
              action: {
                label: summary.action,
                run: () => {
                  this.dialog.closeAll();
                  if (one?.id) void this.editSource(one.id, one.name);
                  else void this.router.navigateByUrl('/sources');
                },
              },
            }
          : {}),
      });
      return;
    }
    const notice = collectionEndedNotice(run);
    this.notifier.notify({
      tone: notice.tone,
      title: notice.title,
      text: notice.text,
      action:
        notice.action === 'view'
          ? {
              label: 'View run',
              run: () => {
                this.dialog.closeAll();
                void this.router.navigateByUrl(`/runs?run=${encodeURIComponent(run.id)}`);
              },
            }
          : {
              label: 'Retry now',
              run: () =>
                void this.runNow((id) =>
                  this.watcher.track({ id, started_at: new Date().toISOString() }),
                ),
            },
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

  /**
   * The countdown moves on; once the next collection's time has passed, the status says whether
   * that run is in progress, for the watcher to follow on any screen (L2-076 AC5), and the
   * schedule is read again for the run after it. Until the schedule names a later time, this is
   * asked again on each tick.
   */
  private tick(): void {
    const now = new Date();
    this.now.set(now);
    const schedule = this.schedule();
    if (!schedule || now < new Date(schedule.next_run_at)) return;
    const signal = this.controller.signal;
    Promise.all([this.api.status(signal), this.api.schedule(signal)])
      .then(([status, next]) => {
        this.watcher.track(status.current_run);
        this.schedule.set(next);
      })
      // A failed check is made again on the next tick.
      .catch(() => undefined);
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
