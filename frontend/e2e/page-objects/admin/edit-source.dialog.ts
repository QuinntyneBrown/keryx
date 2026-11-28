import type { Locator, Page } from '@playwright/test';

/** The "Edit source" dialog (docs/mocks/dialogs/edit-source/). */
export class EditSourceDialog {
  readonly root: Locator;
  readonly title: Locator;
  readonly description: Locator;
  readonly close: Locator;
  /** The alert about the last failed fetch, above the fields. */
  readonly lastFetch: Locator;
  readonly lastFetchTitle: Locator;
  readonly types: Locator;
  readonly url: Locator;
  readonly urlLabel: Locator;
  readonly urlError: Locator;
  readonly organisation: Locator;
  readonly region: Locator;
  readonly hint: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly submit: Locator;

  constructor(private readonly page: Page) {
    this.root = page.locator('.dialog');
    this.title = this.root.locator('.dialog__title');
    this.description = this.root.locator('.dialog__description');
    this.close = this.root.locator('.dialog__head .icon-btn');
    this.lastFetch = this.root.locator('.dialog__body .alert--danger').filter({
      hasText: 'Last fetch failed',
    });
    this.lastFetchTitle = this.lastFetch.locator('.alert__title');
    this.types = this.root.locator('.choice-card');
    this.url = this.root.locator('#source-url');
    this.urlLabel = this.root.locator('label[for="source-url"]');
    this.urlError = this.root.locator('#source-url-error');
    this.organisation = this.root.locator('#source-org');
    this.region = this.root.locator('#source-region');
    this.hint = this.root.locator('.dialog__body .alert').last();
    this.failure = this.root.locator('.dialog__body .alert--danger').filter({
      hasText: 'couldn’t save',
    });
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.submit = this.root.locator('.dialog__foot .btn--primary');
  }

  /** The id of the element that has focus. */
  focusedId(): Promise<string> {
    return this.page.evaluate(() => document.activeElement?.id ?? '');
  }
}
