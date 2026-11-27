import type { Locator, Page } from '@playwright/test';

/** The "Revoke API key" dialog (docs/mocks/dialogs/revoke-api-key/). */
export class RevokeApiKeyDialog {
  readonly container: Locator;
  readonly root: Locator;
  readonly panel: Locator;
  readonly close: Locator;
  readonly body: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly confirm: Locator;

  constructor(page: Page) {
    this.container = page.getByRole('dialog');
    this.root = page.locator('.dialog');
    this.panel = this.root.locator('.dialog__panel');
    this.close = this.root.locator('.dialog__head .icon-btn');
    this.body = this.root.locator('.dialog__body > p');
    this.failure = this.root.locator('.dialog__body .alert--danger');
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.confirm = this.root.locator('.dialog__foot .btn--danger-solid');
  }
}
