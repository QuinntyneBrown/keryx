import type { Locator, Page } from '@playwright/test';

/** Overview (docs/mocks/pages/overview/): the status card with its stats. */
export class OverviewPage {
  readonly heading: Locator;
  readonly statusCard: Locator;
  readonly statusIcon: Locator;
  readonly eyebrow: Locator;
  readonly headline: Locator;
  readonly meta: Locator;
  readonly statLabels: Locator;
  readonly statValues: Locator;
  readonly statSubs: Locator;
  readonly navLink: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.locator('main h1');
    this.statusCard = page.locator('main .status-card');
    this.statusIcon = this.statusCard.locator('.status-card__icon');
    this.eyebrow = this.statusCard.locator('.status-card__eyebrow');
    this.headline = this.statusCard.locator('.status-card__headline');
    this.meta = this.statusCard.locator('.status-card__meta');
    this.statLabels = this.statusCard.locator('.stat__label');
    this.statValues = this.statusCard.locator('.stat__value');
    this.statSubs = this.statusCard.locator('.stat__sub');
    this.navLink = page.getByRole('navigation', { name: 'Primary' }).getByRole('link', {
      name: 'Overview',
    });
  }

  async open(): Promise<void> {
    await this.page.goto('/');
  }
}
