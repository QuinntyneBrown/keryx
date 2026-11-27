import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import type { Notice, Notifier } from '@kx/api';

/** A notice on screen, with the key it is tracked and dismissed by. */
export interface ShownNotice {
  key: number;
  notice: Notice;
}

/** At most this many toasts at once: a new one pushes out the oldest. */
export const MAX_TOASTS = 3;
/** How long a toast stays when nobody dismisses it. */
export const TOAST_MS = 8000;

/**
 * The toasts the shell shows (L2-075). Bounded to three; each leaves after eight seconds or when
 * dismissed, and every timer ends with the shell.
 */
@Injectable()
export class NotifierStore implements Notifier {
  private readonly shown = signal<ShownNotice[]>([]);
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();
  private next = 0;

  readonly notices = this.shown.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      for (const timer of this.timers.values()) clearTimeout(timer);
      this.timers.clear();
    });
  }

  notify(notice: Notice): void {
    const key = this.next++;
    const kept = [...this.shown(), { key, notice }];
    for (const old of kept.slice(0, Math.max(0, kept.length - MAX_TOASTS))) this.forget(old.key);
    this.shown.set(kept.slice(-MAX_TOASTS));
    this.timers.set(
      key,
      setTimeout(() => this.dismiss(key), TOAST_MS),
    );
  }

  dismiss(key: number): void {
    this.forget(key);
    this.shown.update((notices) => notices.filter((shown) => shown.key !== key));
  }

  act(shown: ShownNotice): void {
    this.dismiss(shown.key);
    shown.notice.action?.run();
  }

  private forget(key: number): void {
    clearTimeout(this.timers.get(key));
    this.timers.delete(key);
  }
}
