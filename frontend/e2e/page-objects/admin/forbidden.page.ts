import type { Page } from '@playwright/test';

export class ForbiddenPage {
  constructor(private readonly page: Page) {}
  get heading() {
    return this.page.getByRole('heading', { level: 1 });
  }
  get navigation() {
    return this.page.getByRole('navigation');
  }
  get reference() {
    return this.page.locator('.empty__ref');
  }
  async switchAccount() {
    await this.page.getByRole('button', { name: 'Sign in with a different account' }).click();
  }
}
