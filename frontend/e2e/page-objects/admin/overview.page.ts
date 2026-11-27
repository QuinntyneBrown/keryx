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
  readonly chartCaption: Locator;
  readonly chart: Locator;
  readonly bars: Locator;

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
    this.chartCaption = this.statusCard.locator('.status-card__history .text-caption');
    this.chart = this.statusCard.locator('ol.run-chart');
    this.bars = this.chart.locator('.run-chart__bar');
    this.navLink = page.getByRole('navigation', { name: 'Primary' }).getByRole('link', {
      name: 'Overview',
    });
  }

  /** What each bar says to a screen reader, oldest first. */
  barLabels(): Promise<string[]> {
    return this.bars.locator('.visually-hidden').allTextContents();
  }

  /** Each bar's outcome, oldest first. */
  barStatuses(): Promise<(string | null)[]> {
    return this.bars.evaluateAll((bars) => bars.map((bar) => bar.getAttribute('data-status')));
  }

  /** Each bar's drawn height in pixels, oldest first. */
  barHeights(): Promise<number[]> {
    return this.bars.evaluateAll((bars) =>
      bars.map((bar) => Math.round(bar.getBoundingClientRect().height)),
    );
  }

  /** Whether every bar lies inside the card, with nothing to scroll sideways. */
  chartFits(): Promise<boolean> {
    return this.statusCard.evaluate((card) => {
      const box = card.getBoundingClientRect();
      const inside = [...card.querySelectorAll('.run-chart__bar')].every((bar) => {
        const r = bar.getBoundingClientRect();
        return r.left >= box.left && r.right <= box.right && r.width > 0;
      });
      return inside && document.documentElement.scrollWidth <= window.innerWidth;
    });
  }

  async open(): Promise<void> {
    await this.page.goto('/');
  }
}
