import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ApiProblem, SETTINGS_API, type SettingsDto } from '@kx/api';
import {
  draftOf,
  settingsEqual,
  settingsOf,
  settingsValidation,
  skeletonDelay,
  type SettingsDraft,
  type SettingsError,
} from '@kx/domain';

export type SettingsState =
  | { status: 'loading' }
  | { status: 'ready'; saved: SettingsDto }
  | { status: 'error'; reference: string | null };

/**
 * Where the last save stands (L2-071): sending, confirmed, failed with a reference, or refused
 * because the settings changed elsewhere since they were loaded.
 */
export type SaveState =
  | { status: 'idle' }
  | { status: 'saving' }
  | { status: 'saved'; at: Date }
  | { status: 'failed'; reference: string | null }
  | { status: 'conflict' };

/** How long the bar confirms a save (L2-071 AC4). */
export const SAVED_FOR_MS = 8_000;

/**
 * The Settings screen's saved settings and the entries on screen (the draft), held only while
 * the screen is shown. A load cancels the one in flight, and leaving the screen cancels whatever
 * is pending, the confirmation's timer included. Provided on the screen, so it ends with the
 * visit.
 */
@Injectable()
export class SettingsStore {
  private readonly api = inject(SETTINGS_API);
  private controller: AbortController | null = null;
  private readonly current = signal<SettingsState>({ status: 'loading' });
  private readonly entries = signal<SettingsDraft | null>(null);
  private readonly skeleton = skeletonDelay();
  private confirmation: ReturnType<typeof setTimeout> | undefined;

  readonly state = this.current.asReadonly();
  readonly draft = this.entries.asReadonly();
  /** A load has had no answer for 300 ms: skeleton sections stand in (L2-071 AC8). */
  readonly slow = signal(false);
  /** The fields the last save found out of range, in the order of the form (L2-071 AC3). */
  readonly errors = signal<SettingsError[]>([]);
  readonly saveState = signal<SaveState>({ status: 'idle' });
  /** An entry differs from its saved value (L2-071 AC1). */
  readonly dirty = computed(() => {
    const state = this.current();
    const draft = this.entries();
    return state.status === 'ready' && !!draft && !settingsEqual(draft, draftOf(state.saved));
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.skeleton.cancel();
      clearTimeout(this.confirmation);
    });
  }

  async load(): Promise<void> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.current.set({ status: 'loading' });
    this.settle({ status: 'idle' });
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

  /** Returns every entry to its saved value (L2-071 AC2). */
  discard(): void {
    const state = this.current();
    if (state.status === 'ready') this.entries.set(draftOf(state.saved));
    this.errors.set([]);
    this.settle({ status: 'idle' });
  }

  /**
   * Saves the entries with the version they were loaded at, in one request (L2-071 AC4). With an
   * entry out of range nothing is sent and the errors say which (AC3): then it resolves false.
   */
  async save(): Promise<boolean> {
    const state = this.current();
    const draft = this.entries();
    if (state.status !== 'ready' || !draft) return true;
    const errors = settingsValidation(draft);
    this.errors.set(errors);
    if (errors.length) return false;
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.settle({ status: 'saving' });
    try {
      const saved = await this.api.saveSettings(
        settingsOf(draft, state.saved.version),
        controller.signal,
      );
      if (controller.signal.aborted) return true;
      this.current.set({ status: 'ready', saved });
      this.entries.set(draftOf(saved));
      this.settle({ status: 'saved', at: new Date() });
      this.confirmation = setTimeout(() => this.settle({ status: 'idle' }), SAVED_FOR_MS);
    } catch (error) {
      if (controller.signal.aborted) return true;
      this.settle(
        error instanceof ApiProblem && error.code === 'settings_changed'
          ? { status: 'conflict' }
          : {
              status: 'failed',
              reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
            },
      );
    }
    return true;
  }

  /** Changes one entry of the draft; it ends a save's confirmation (L2-071 AC4). */
  set<K extends keyof SettingsDraft>(field: K, value: SettingsDraft[K]): void {
    this.entries.update((draft) => (draft ? { ...draft, [field]: value } : draft));
    if (this.saveState().status === 'saved') this.settle({ status: 'idle' });
  }

  private settle(state: SaveState): void {
    clearTimeout(this.confirmation);
    this.saveState.set(state);
  }
}
