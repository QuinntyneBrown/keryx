import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  ApiProblem,
  NOTIFIER,
  SOURCES_API,
  type SourceKind,
  type SourceListItem,
  type SourcePage,
} from '@kx/api';
import { skeletonDelay, sourceTestNotice } from '@kx/domain';

export type SourcesState =
  | { status: 'loading' }
  | { status: 'ready'; page: SourcePage }
  | { status: 'error'; reference: string | null };

/** The page size the Sources screen asks for (L2-062). */
export const SOURCES_PAGE_SIZE = 12;

/**
 * The Sources screen's catalogue: one page at a time, held only while the screen is shown. A new
 * load cancels the one in flight, and leaving the screen cancels whatever is pending. Provided
 * on the screen, so it ends with the visit.
 */
@Injectable()
export class SourcesStore {
  private readonly api = inject(SOURCES_API);
  private readonly notifier = inject(NOTIFIER);
  /** Ends what the screen started that outlives one load, such as a test fetch. */
  private readonly screen = new AbortController();
  private controller: AbortController | null = null;
  private readonly current = signal<SourcesState>({ status: 'loading' });

  readonly state = this.current.asReadonly();
  private readonly skeleton = skeletonDelay();
  /** A load has had no answer for 300 ms: skeleton rows stand in (L2-073 AC1). */
  readonly slow = signal(false);
  /** The page and kind shown, or asked for: a reload reads them again. */
  private page = 1;
  private kind: SourceKind | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.screen.abort();
      this.skeleton.cancel();
    });
  }

  async load(page = this.page, kind = this.kind): Promise<void> {
    this.page = page;
    this.kind = kind;
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.current.set({ status: 'loading' });
    this.slow.set(false);
    this.skeleton.run(() => this.slow.set(true));
    try {
      const result = await this.api.list(
        { page, pageSize: SOURCES_PAGE_SIZE, ...(kind ? { kind } : {}) },
        controller.signal,
      );
      if (!controller.signal.aborted) this.current.set({ status: 'ready', page: result });
    } catch (error) {
      if (controller.signal.aborted) return;
      this.current.set({
        status: 'error',
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      });
    }
  }

  /**
   * "Test fetch now" (L2-063 AC3): tests the source and says how it went, leaving the rows as
   * they are. A source removed meanwhile is said, and the list read again; any other refusal
   * says the test did not run, with "Try again".
   */
  async testSource(source: SourceListItem): Promise<void> {
    try {
      const result = await this.api.testSource(source.id, this.screen.signal);
      this.notifier.notify(sourceTestNotice(source.kind, source.organization_name, result));
    } catch (error) {
      if (this.screen.signal.aborted) return;
      if (error instanceof ApiProblem && error.code === 'source_not_found') {
        this.notifier.notify({
          tone: 'danger',
          title: 'Source not found',
          text: `${source.organization_name} was removed meanwhile, so the list is read again.`,
        });
        void this.load();
        return;
      }
      const reference = error instanceof ApiProblem ? error.reference : null;
      this.notifier.notify({
        tone: 'danger',
        title: 'Test fetch didn’t run',
        text: `Keryx couldn’t reach its API just now.${reference ? ` Reference ${reference}` : ''}`,
        action: { label: 'Try again', run: () => void this.testSource(source) },
      });
    }
  }
}
