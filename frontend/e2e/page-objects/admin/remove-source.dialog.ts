import type { Locator, Page } from '@playwright/test';

/** The "Remove source" confirmation (docs/mocks/dialogs/remove-source/). */
export class RemoveSourceDialog {
  readonly root: Locator;
  readonly title: Locator;
  readonly description: Locator;
  readonly text: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly confirm: Locator;

  constructor(page: Page) {
    this.root = page.locator('.dialog');
    this.title = this.root.locator('.dialog__title');
    this.description = this.root.locator('.dialog__description');
    this.text = this.root.locator('.dialog__body > p');
    this.failure = this.root.locator('.dialog__body .alert--danger');
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.confirm = this.root.locator('.dialog__foot .btn--danger-solid');
  }
}
