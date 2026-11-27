import type { Locator, Page } from '@playwright/test';

/** The "New API key" dialog (docs/mocks/dialogs/new-api-key/). */
export class NewApiKeyDialog {
  readonly container: Locator;
  readonly root: Locator;
  readonly close: Locator;
  readonly name: Locator;
  readonly nameLabel: Locator;
  readonly nameHelp: Locator;
  readonly nameError: Locator;
  readonly scopeGroup: Locator;
  readonly scopeLegend: Locator;
  readonly scopes: Locator;
  readonly scopeTexts: Locator;
  readonly scopeError: Locator;
  readonly note: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly submit: Locator;

  constructor(private readonly page: Page) {
    this.container = page.getByRole('dialog');
    this.root = page.locator('.dialog');
    this.close = this.root.locator('.dialog__head .icon-btn');
    this.name = this.root.locator('#key-name');
    this.nameLabel = this.root.locator('label[for="key-name"]');
    this.nameHelp = this.root.locator('#key-name-help');
    this.nameError = this.root.locator('#key-name-error');
    this.scopeGroup = this.root.getByRole('group', { name: 'Scopes' });
    this.scopeLegend = this.root.locator('fieldset legend');
    this.scopes = this.root.locator('.check input[type="checkbox"]');
    this.scopeTexts = this.root.locator('.check .check__text');
    this.scopeError = this.root.locator('#scope-error');
    this.note = this.root.locator('.dialog__body .alert--neutral');
    this.failure = this.root.locator('.dialog__body .alert--danger');
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.submit = this.root.locator('.dialog__foot .btn--primary');
  }

  scope(value: string): Locator {
    return this.root.getByRole('checkbox', { name: value });
  }

  focusedId(): Promise<string> {
    return this.page.evaluate(() => document.activeElement?.id ?? '');
  }
}
