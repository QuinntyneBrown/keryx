import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ApiProblem, SETTINGS_API, type SettingsDto } from '@kx/api';
import { draftOf, skeletonDelay, type SettingsDraft } from '@kx/domain';

export type SettingsState =
  | { status: 'loading' }
  | { status: 'ready'; saved: SettingsDto }
  | { status: 'error'; reference: string | null };

/**
 * The Settings screen's saved settings and the entries on screen (the draft), held only while
 * the screen is shown. A load cancels the one in flight, and leaving the screen cancels whatever
 * is pending. Provided on the screen, so it ends with the visit.
 */
@Injectable()
export class SettingsStore {
  private readonly api = inject(SETTINGS_API);
  private controller: AbortController | null = null;
  private readonly current = signal<SettingsState>({ status: 'loading' });
  private readonly entries = signal<SettingsDraft | null>(null);
  private readonly skeleton = skeletonDelay();

  readonly state = this.current.asReadonly();
  readonly draft = this.entries.asReadonly();
  /** A load has had no answer for 300 ms: skeleton sections stand in (L2-071 AC8). */
  readonly slow = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.skeleton.cancel();
    });
  }

  async load(): Promise<void> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.current.set({ status: 'loading' });
    this.slow.set(false);
    this.skeleton.run(() => this.slow.set(true));
    try {
      const saved = await this.api.getSettings(controller.signal);
      if (controller.signal.aborted) return;
      this.entries.set(draftOf(saved));
      this.current.set({ status: 'ready', saved });
    } catch (error) {
      if (controller.signal.aborted) return;
      this.current.set({
        status: 'error',
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      });
    }
  }

  /** Changes one entry of the draft. */
  set<K extends keyof SettingsDraft>(field: K, value: SettingsDraft[K]): void {
    this.entries.update((draft) => (draft ? { ...draft, [field]: value } : draft));
  }
}
