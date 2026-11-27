import { Clipboard } from '@angular/cdk/clipboard';
import { Component, computed, inject, Injector } from '@angular/core';
import { Router } from '@angular/router';
import { API_CLIENTS_API, NOTIFIER, type ApiClientDto, type CreatedApiClientDto } from '@kx/api';
import {
  Badge,
  Btn,
  Callout,
  CopyField,
  Empty,
  Icon,
  PageBody,
  PageHeader,
  Table,
  Tag,
  type TableColumn,
} from '@kx/components';
import { ApiStore } from '../../clients/api-store';
import { HttpApiClientsApi } from '../../clients/http-api-clients-api';

/**
 * API (docs/mocks/pages/api/). Journey 7 builds its header with "New API key", the key-created
 * callout, the empty and error states, and a table of each client's product, key and scopes with
 * Revoke; the usage summary, usage columns and endpoints arrive with journey 21 (report-api-usage).
 */
@Component({
  selector: 'kx-api',
  imports: [Badge, Btn, Callout, CopyField, Empty, Icon, PageBody, PageHeader, Table, Tag],
  providers: [{ provide: API_CLIENTS_API, useClass: HttpApiClientsApi }, ApiStore],
  templateUrl: './api.html',
  styleUrl: './api.scss',
})
export default class Api {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly notifier = inject(NOTIFIER);
  private readonly clipboard = inject(Clipboard);
  protected readonly store = inject(ApiStore);

  protected readonly columns: TableColumn[] = [
    { key: 'product', label: 'Product', primary: true },
    { key: 'key', label: 'Key', wide: true },
    { key: 'scopes', label: 'Scopes', wide: true },
    { key: 'actions', label: 'Actions', actionsWide: true, hiddenLabel: true, numeric: true },
  ];

  protected readonly rows = computed(() => {
    const state = this.store.state();
    if (state.status !== 'ready') return [];
    const newest = this.store.newClientId();
    return state.page.items.map((client) => ({
      client,
      prefix: `${client.key_prefix}…`,
      isNew: client.id === newest,
      revoked: client.revoked_at !== null,
    }));
  });

  protected readonly rowKey = (row: unknown) => (row as { client: ApiClientDto }).client.id;
  protected readonly rowModifier = (row: unknown) =>
    (row as { revoked: boolean }).revoked ? 'revoked' : null;

  constructor() {
    void this.store.load();
  }

  /**
   * Opens "New API key" with this screen's injector. The CDK dialog and the dialog's code load
   * when it is first opened, not with the screen. Focus starts in the name.
   */
  protected async newKey(): Promise<void> {
    const opener = document.activeElement;
    const [{ Dialog }, dialog] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./new-api-key-dialog/new-api-key-dialog'),
    ]);
    const ref = this.injector.get(Dialog).open<CreatedApiClientDto>(dialog.NewApiKeyDialog, {
      injector: this.injector,
      ariaModal: true,
      ariaLabelledBy: dialog.NEW_API_KEY_TITLE_ID,
      ariaDescribedBy: dialog.NEW_API_KEY_DESCRIPTION_ID,
      autoFocus: '#key-name',
      restoreFocus: opener instanceof HTMLElement ? opener : true,
      backdropClass: 'kx-backdrop',
    });
    ref.closed.subscribe((created) => {
      if (created) this.store.created(created);
    });
  }

  /** Copies the revealed key; a refused clipboard leaves it to select by hand (L2-068 AC4). */
  protected copy(key: string): void {
    this.notifier.notify(
      this.clipboard.copy(key)
        ? { tone: 'success', title: 'Key copied' }
        : {
            tone: 'danger',
            title: 'Keryx couldn’t copy the key',
            text: 'Your browser blocked the clipboard. Select the key and copy it by hand.',
          },
    );
  }

  /**
   * Opens "Revoke API key" for a client, focus on "Cancel". A revoke marks the row and warns,
   * offering a new key and no Undo; focus moves to "New API key", since the row's Revoke is gone
   * (L2-069 AC2).
   */
  protected async revoke(client: ApiClientDto, newKeyButton: Btn): Promise<void> {
    const opener = document.activeElement;
    const [{ Dialog }, dialog] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./revoke-api-key-dialog/revoke-api-key-dialog'),
    ]);
    const ref = this.injector.get(Dialog).open<ApiClientDto>(dialog.RevokeApiKeyDialog, {
      injector: this.injector,
      data: client,
      ariaModal: true,
      ariaLabelledBy: dialog.REVOKE_API_KEY_TITLE_ID,
      ariaDescribedBy: dialog.REVOKE_API_KEY_DESCRIPTION_ID,
      autoFocus: dialog.REVOKE_API_KEY_AUTO_FOCUS,
      restoreFocus: opener instanceof HTMLElement ? opener : true,
      backdropClass: 'kx-backdrop',
    });
    ref.closed.subscribe((revoked) => {
      if (!revoked) return;
      this.store.revoked(revoked);
      newKeyButton.focus();
      this.notifier.notify({
        tone: 'warning',
        title: 'Key revoked',
        text: `${revoked.product_name} now gets 401 Unauthorized. Create a new key to reconnect it.`,
        action: { label: 'New API key', run: () => void this.newKey() },
      });
    });
  }

  protected retry(): void {
    void this.store.load();
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
