import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  API_CLIENTS_API,
  API_USAGE_API,
  ApiProblem,
  type ApiUsageDto,
  type ApiClientDto,
  type ApiClientPage,
  type CreatedApiClientDto,
} from '@kx/api';

export type ApiClientsState =
  | { status: 'loading' }
  | { status: 'ready'; page: ApiClientPage }
  | { status: 'error'; reference: string | null };

export type ApiUsageState =
  | { status: 'loading' }
  | { status: 'ready'; usage: ApiUsageDto }
  | { status: 'error'; reference: string | null };

/** A failed read's reference, the response's trace id, for staff to quote. */
function referenceOf(error: unknown): string | null {
  return error instanceof ApiProblem ? (error.reference ?? null) : null;
}

/** A key just created, shown once (L2-068 AC3). */
export interface RevealedKey {
  productName: string;
  key: string;
}

/**
 * The API screen's clients and the key just created. Provided on the screen, so it ends with the
 * visit: then the key is dropped from memory and so from the DOM (L2-068 AC5). The key is never
 * written to the URL, storage, a log or telemetry.
 */
@Injectable()
export class ApiStore {
  private readonly api = inject(API_CLIENTS_API);
  private readonly usageApi = inject(API_USAGE_API);
  private controller: AbortController | null = null;
  private readonly current = signal<ApiClientsState>({ status: 'loading' });
  private readonly usageState = signal<ApiUsageState>({ status: 'loading' });
  private readonly revealed = signal<RevealedKey | null>(null);
  private readonly newest = signal<string | null>(null);

  readonly state = this.current.asReadonly();
  /** The whole API's usage, read beside the clients: either can fail on its own. */
  readonly usage = this.usageState.asReadonly();
  readonly revealedKey = this.revealed.asReadonly();
  /** The id of the client created on this visit, badged "New". */
  readonly newClientId = this.newest.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.revealed.set(null);
    });
  }

  /** Reads the clients and the usage in parallel. */
  async load(): Promise<void> {
    this.controller?.abort();
    const controller = new AbortController();
    this.controller = controller;
    this.current.set({ status: 'loading' });
    this.usageState.set({ status: 'loading' });
    await Promise.all([this.loadClients(controller), this.loadUsage(controller)]);
  }

  private async loadUsage(controller: AbortController): Promise<void> {
    try {
      const usage = await this.usageApi.get(controller.signal);
      if (!controller.signal.aborted) this.usageState.set({ status: 'ready', usage });
    } catch (error) {
      if (controller.signal.aborted) return;
      this.usageState.set({ status: 'error', reference: referenceOf(error) });
    }
  }

  private async loadClients(controller: AbortController): Promise<void> {
    try {
      const page = await this.api.list(1, controller.signal);
      if (!controller.signal.aborted) this.current.set({ status: 'ready', page });
    } catch (error) {
      if (controller.signal.aborted) return;
      this.current.set({ status: 'error', reference: referenceOf(error) });
    }
  }

  /** A key was created: its client is first in the table, and the key is revealed once. */
  created({ client, key }: CreatedApiClientDto): void {
    const state = this.current();
    if (state.status === 'ready') {
      const { page } = state;
      this.current.set({
        status: 'ready',
        page: { ...page, items: [client, ...page.items], total: page.total + 1 },
      });
    }
    this.newest.set(client.id);
    this.revealed.set({ productName: client.product_name, key });
  }

  /** A key was revoked: its row stays, marked revoked (L2-036 AC3). */
  revoked(client: ApiClientDto): void {
    const state = this.current();
    if (state.status !== 'ready') return;
    const { page } = state;
    this.current.set({
      status: 'ready',
      page: { ...page, items: page.items.map((item) => (item.id === client.id ? client : item)) },
    });
  }
}
