import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { OVERVIEW_API, type ShellSchedule } from '@kx/api';

/** How often the countdown to the next run is worked out again. */
const CLOCK_MS = 30_000;

/**
 * The shell's schedule (Admin shell design): the next collection and the failing sources, read
 * once for the signed-in visit and shared by the screens that show it, with the time the
 * countdown reads. Provided on the shell's route, as the session's stores are (ADR 0005).
 */
@Injectable()
export class ScheduleStore {
  private readonly api = inject(OVERVIEW_API);
  private readonly controller = new AbortController();
  private requested = false;

  /** The schedule once read; null before then or when it failed. */
  readonly schedule = signal<ShellSchedule | null>(null);
  readonly now = signal(new Date());

  constructor() {
    const clock = setInterval(() => this.now.set(new Date()), CLOCK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(clock);
      this.controller.abort();
    });
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
