import type { Locator, Page } from '@playwright/test';

/** "Leave without saving?" (docs/mocks/dialogs/leave-settings/). */
export class LeaveSettingsDialog {
  readonly root: Locator;
  readonly title: Locator;
  readonly description: Locator;
  readonly text: Locator;
  readonly stay: Locator;
  readonly leave: Locator;

  constructor(page: Page) {
    this.root = page.locator('.dialog');
    this.title = this.root.locator('.dialog__title');
    this.description = this.root.locator('.dialog__description');
    this.text = this.root.locator('.dialog__body > p');
    this.stay = this.root.locator('.dialog__foot .btn', { hasText: 'Stay' });
    this.leave = this.root.locator('.dialog__foot .btn--danger-solid');
  }
}
