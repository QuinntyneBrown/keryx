import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ApiProblem, SOURCES_API, type SourcePage } from '@kx/api';

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
  private controller: AbortController | null = null;
  private readonly current = signal<SourcesState>({ status: 'loading' });

  readonly state = this.current.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }

  async load(page = 1): Promise<void> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.current.set({ status: 'loading' });
    try {
      const result = await this.api.list({ page, pageSize: SOURCES_PAGE_SIZE }, controller.signal);
      if (!controller.signal.aborted) this.current.set({ status: 'ready', page: result });
    } catch (error) {
      if (controller.signal.aborted) return;
      this.current.set({
        status: 'error',
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      });
    }
  }
}
