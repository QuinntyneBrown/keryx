import type { Locator, Page } from '@playwright/test';

/** "Add a topic", "Add an exclusion" and "Add a focus area" (docs/mocks/dialogs/add-keyword/). */
export class AddKeywordDialog {
  readonly root: Locator;
  readonly title: Locator;
  readonly description: Locator;
  readonly label: Locator;
  readonly field: Locator;
  readonly help: Locator;
  readonly error: Locator;
  readonly cancel: Locator;
  readonly submit: Locator;

  constructor(page: Page) {
    this.root = page.locator('.dialog');
    this.title = this.root.locator('.dialog__title');
    this.description = this.root.locator('.dialog__description');
    this.label = this.root.locator('.field__label');
    this.field = this.root.locator('.dialog__body input');
    this.help = this.root.locator('.field__help');
    this.error = this.root.locator('.field__error');
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.submit = this.root.locator('.dialog__foot .btn--primary');
  }
}
