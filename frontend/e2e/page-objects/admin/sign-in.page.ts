import type { Page } from '@playwright/test';

export class SignInPage {
  constructor(readonly page: Page) {}

  get heading() {
    return this.page.getByRole('heading', { level: 1 });
  }

  get button() {
    return this.page.getByRole('button');
  }

  get notice() {
    return this.page.locator('.alert');
  }

  get navigation() {
    return this.page.getByRole('navigation');
  }

  async open(path: string) {
    await this.page.goto(path);
  }
}
