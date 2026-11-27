import type { Locator, Page } from '@playwright/test';

/** The "Add a source" dialog (docs/mocks/dialogs/add-source/). */
export class AddSourceDialog {
  readonly root: Locator;
  readonly container: Locator;
  readonly panel: Locator;
  readonly title: Locator;
  readonly description: Locator;
  readonly close: Locator;
  readonly typeLegend: Locator;
  readonly types: Locator;
  readonly url: Locator;
  readonly urlLabel: Locator;
  readonly urlError: Locator;
  readonly organisation: Locator;
  readonly organisationError: Locator;
  readonly region: Locator;
  readonly regionError: Locator;
  readonly regionOptions: Locator;
  readonly hint: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly submit: Locator;

  constructor(private readonly page: Page) {
    this.container = page.getByRole('dialog');
    this.root = page.locator('.dialog');
    this.panel = this.root.locator('.dialog__panel');
    this.title = this.root.locator('.dialog__title');
    this.description = this.root.locator('.dialog__description');
    this.close = this.root.locator('.dialog__head .icon-btn');
    this.typeLegend = this.root.locator('fieldset legend');
    this.types = this.root.locator('.choice-card input[type="radio"]');
    this.url = this.root.locator('#source-url');
    this.urlLabel = this.root.locator('label[for="source-url"]');
    this.urlError = this.root.locator('#source-url-error');
    this.organisation = this.root.locator('#source-org');
    this.organisationError = this.root.locator('#source-org-error');
    this.region = this.root.locator('#source-region');
    this.regionOptions = this.region.locator('option');
    this.regionError = this.root.locator('#source-region-error');
    this.hint = this.root.locator('.dialog__body .alert').last();
    this.failure = this.root.locator('.dialog__body .alert--danger');
    this.cancel = this.root.locator('.dialog__foot .btn', { hasText: 'Cancel' });
    this.submit = this.root.locator('.dialog__foot .btn--primary');
  }

  type(label: string): Locator {
    return this.root.getByRole('radio', { name: label });
  }

  /** The labels of the type radios, in order. */
  typeLabels(): Promise<string[]> {
    return this.root
      .locator('.choice-card')
      .allTextContents()
      .then((labels) => labels.map((l) => l.trim()));
  }

  /** The id of the element that has focus. */
  focusedId(): Promise<string> {
    return this.page.evaluate(() => document.activeElement?.id ?? '');
  }
}
