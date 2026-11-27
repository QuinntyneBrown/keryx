import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { API_CLIENTS_API, ApiProblem, type ApiClientDto } from '@kx/api';
import { Alert, Btn, Dialog } from '@kx/components';

export const REVOKE_API_KEY_TITLE_ID = 'revoke-api-key-title';
export const REVOKE_API_KEY_DESCRIPTION_ID = 'revoke-api-key-description';

/** Where focus starts: "Cancel", because revoking has no Undo (L2-069 AC1). */
export const REVOKE_API_KEY_AUTO_FOCUS = '.dialog__foot .btn';

/**
 * "Revoke API key" (docs/mocks/dialogs/revoke-api-key/, L2-069), opened by the API screen in a
 * CDK dialog with the client to revoke. It closes with the revoked client.
 */
@Component({
  selector: 'kx-revoke-api-key-dialog',
  imports: [Alert, Btn, Dialog],
  templateUrl: './revoke-api-key-dialog.html',
  styleUrl: './revoke-api-key-dialog.scss',
})
export class RevokeApiKeyDialog {
  private readonly api = inject(API_CLIENTS_API);
  private readonly ref = inject<DialogRef<ApiClientDto>>(DialogRef);
  private readonly abort = new AbortController();

  protected readonly client = inject<ApiClientDto>(DIALOG_DATA);
  protected readonly titleId = REVOKE_API_KEY_TITLE_ID;
  protected readonly descriptionId = REVOKE_API_KEY_DESCRIPTION_ID;
  /** A request is in flight: nothing closes the dialog and one request is sent (L2-069 AC3). */
  protected readonly busy = signal(false);
  /** The last request failed; the key is still active (L2-069 AC4). */
  protected readonly failure = signal<{ reference: string | null } | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
  }

  protected async confirm(): Promise<void> {
    this.busy.set(true);
    this.failure.set(null);
    this.ref.disableClose = true;
    try {
      const revoked = await this.api.revoke(this.client.id, this.abort.signal);
      this.ref.disableClose = false;
      this.ref.close(revoked);
    } catch (error) {
      if (this.abort.signal.aborted) return;
      this.ref.disableClose = false;
      this.busy.set(false);
      this.failure.set({
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      });
    }
  }

  protected close(): void {
    if (this.busy()) return;
    this.ref.close();
  }
}
