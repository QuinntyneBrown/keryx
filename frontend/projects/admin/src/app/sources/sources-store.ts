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
   * Reads the shown page again in place, after a change made in a dialog (L2-065 AC2): the rows
   * stay until the answer replaces them, so the row and the focus on it remain. If the read
   * fails, the list loads as on opening, and says so if that fails too.
   */
  async refresh(): Promise<void> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    try {
      const result = await this.api.list(
        { page: this.page, pageSize: SOURCES_PAGE_SIZE, ...(this.kind ? { kind: this.kind } : {}) },
        controller.signal,
      );
      if (!controller.signal.aborted) this.current.set({ status: 'ready', page: result });
    } catch {
      if (!controller.signal.aborted) await this.load();
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
        this.gone(source);
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

  /**
   * The source a dialog is about to show, with its article count. One removed meanwhile is said
   * and the list read again (null); if it cannot be read, the dialog goes ahead without it.
   */
  async detail(source: SourceListItem): Promise<{ articles: number | null } | null> {
    try {
      const detail = await this.api.source(source.id, this.screen.signal);
      return { articles: detail.article_count };
    } catch (error) {
      if (this.screen.signal.aborted) return null;
      if (error instanceof ApiProblem && error.code === 'source_not_found') {
        this.gone(source);
        return null;
      }
      return { articles: null };
    }
  }

  /**
   * Removes a source (L2-065 AC4, AC5): its row goes and the counts follow, with "Undo" in the
   * notification. A failure is said with "Try again", and its reference is returned for the
   * dialog; null when it was removed.
   */
  async remove(source: SourceListItem): Promise<{ reference: string | null } | null> {
    try {
      await this.api.remove(source.id, this.screen.signal);
    } catch (error) {
      if (this.screen.signal.aborted) return null;
      this.notifier.notify({
        tone: 'danger',
        title: 'Keryx couldn’t remove the source',
        text: `${source.organization_name} is still being checked.`,
        action: { label: 'Try again', run: () => void this.remove(source) },
      });
      return { reference: error instanceof ApiProblem ? (error.reference ?? null) : null };
    }
    this.notifier.notify({
      tone: 'info',
      title: 'Source removed',
      text: `${source.organization_name} is no longer checked. Its articles stay in Keryx.`,
      action: { label: 'Undo', run: () => void this.restore(source) },
    });
    await this.refresh();
    return null;
  }

  /** "Undo" of a removal: the source is listed again, or the notification says why not. */
  async restore(source: SourceListItem): Promise<void> {
    try {
      await this.api.restore(source.id, this.screen.signal);
    } catch (error) {
      if (this.screen.signal.aborted) return;
      const exists = error instanceof ApiProblem && error.code === 'source_exists';
      this.notifier.notify({
        tone: 'danger',
        title: 'Keryx couldn’t add the source back',
        text: exists
          ? `${source.organization_name} was added again meanwhile, so it is listed already.`
          : 'Nothing changed. Try again in a moment.',
        ...(exists ? {} : { action: { label: 'Try again', run: () => void this.restore(source) } }),
      });
      return;
    }
    await this.refresh();
  }

  /** A source that is no longer there: it is said, and the list read again. */
  private gone(source: SourceListItem): void {
    this.notifier.notify({
      tone: 'danger',
      title: 'Source not found',
      text: `${source.organization_name} was removed meanwhile, so the list is read again.`,
    });
    void this.load();
  }
}
