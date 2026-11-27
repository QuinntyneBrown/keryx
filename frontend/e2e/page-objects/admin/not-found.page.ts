import type { Locator, Page } from '@playwright/test';

/**
 * The Not found screen (docs/mocks/pages/not-found/default.html): one empty-state card inside
 * the shell, with no navigation item current.
 */
export class NotFoundPage {
  readonly heading: Locator;
  readonly text: Locator;
  readonly overviewLink: Locator;
  readonly sidebar: Locator;
  readonly currentLinks: Locator;

  constructor(private readonly page: Page) {
    const card = page.locator('main .empty.empty--card');
    this.heading = card.locator('h1.empty__title');
    this.text = card.locator('.empty__text');
    this.overviewLink = card.locator('.empty__actions a.btn.btn--primary');
    this.sidebar = page.locator('aside.sidebar');
    this.currentLinks = page.locator('[aria-current="page"]');
  }

  async open(path: string): Promise<void> {
    await this.page.goto(path);
    await this.heading.waitFor();
  }

  title(): Promise<string> {
    return this.page.title();
  }

  path(): string {
    return new URL(this.page.url()).pathname;
  }

  /** Marks the document, so a later check can tell whether it was loaded again. */
  async markDocument(): Promise<void> {
    await this.page.evaluate(() => document.documentElement.setAttribute('data-e2e-kept', ''));
  }

  documentWasKept(): Promise<boolean> {
    return this.page.evaluate(() => document.documentElement.hasAttribute('data-e2e-kept'));
  }
}
