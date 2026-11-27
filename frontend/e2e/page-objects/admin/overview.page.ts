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
  readonly latest: Locator;
  readonly latestHeading: Locator;
  readonly viewAll: Locator;
  readonly latestItems: Locator;
  readonly latestTitles: Locator;
  readonly latestMetas: Locator;
  readonly latestAges: Locator;
  readonly attention: Locator;
  readonly attentionHeading: Locator;
  readonly openCount: Locator;
  readonly issueCards: Locator;
  readonly issueTitles: Locator;
  readonly issueSources: Locator;
  readonly issueMetas: Locator;
  readonly issueSeverities: Locator;
  readonly allClear: Locator;
  /** The whole-page empty or error state. */
  readonly pageState: Locator;
  readonly pageStateTitle: Locator;
  readonly pageStateText: Locator;
  readonly pageStateReference: Locator;
  readonly pageStateActions: Locator;
  /** The notice in Latest articles after a failed run. */
  readonly latestNotice: Locator;
  /** A section's error in place of its content. */
  readonly latestError: Locator;
  readonly attentionError: Locator;
  readonly statusError: Locator;
  readonly chartError: Locator;
  readonly body: Locator;
  readonly loadingStatus: Locator;
  readonly statusSkeletons: Locator;
  readonly latestSkeletonRows: Locator;
  readonly issueSkeletons: Locator;

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
    this.latest = page.locator('main section[aria-labelledby="latest-title"]');
    this.latestHeading = this.latest.locator('h2#latest-title');
    this.viewAll = this.latest.locator('a.section-head__link');
    this.latestItems = this.latest.locator('.list__item');
    this.latestTitles = this.latestItems.locator('.list__title');
    this.latestMetas = this.latestItems.locator('.list__meta');
    this.latestAges = this.latestItems.locator('.list__aside > span:first-child');
    this.attention = page.locator('main section[aria-labelledby="attention-title"]');
    this.attentionHeading = this.attention.locator('h2#attention-title');
    this.openCount = this.attention.locator('.section-head .text-muted');
    this.issueCards = this.attention.locator('.issue-card');
    this.issueTitles = this.issueCards.locator('.issue-card__title');
    this.issueSources = this.issueCards.locator('.issue-card__source');
    this.issueMetas = this.issueCards.locator('.issue-card__meta');
    this.issueSeverities = this.issueCards.locator('.visually-hidden');
    this.allClear = this.attention.locator('.empty');
    this.pageState = page.locator('main .page-body > kx-empty > .empty');
    this.pageStateTitle = this.pageState.locator('.empty__title');
    this.pageStateText = this.pageState.locator('.empty__text');
    this.pageStateReference = this.pageState.locator('.empty__ref');
    this.pageStateActions = this.pageState.locator('.empty__actions .btn');
    this.latestNotice = this.latest.locator('.empty');
    this.latestError = this.latest.locator('.empty--error');
    this.attentionError = this.attention.locator('.empty--error');
    this.statusError = page.locator('main .page-body .stack > kx-empty > .empty--error');
    this.chartError = this.statusCard.locator('.status-card__history .empty--error');
    this.body = page.locator('main .page-body');
    this.loadingStatus = this.body.locator('.visually-hidden[role="status"]');
    this.statusSkeletons = this.statusCard.locator('.skeleton');
    this.latestSkeletonRows = this.latest.locator('.list__item:has(.skeleton)');
    this.issueSkeletons = this.attention.locator('.issue-card:has(.skeleton)');
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

  /** The badge of each latest article, or null where there is none, newest first. */
  latestBadges(): Promise<(string | null)[]> {
    return this.latestItems.evaluateAll((items) =>
      items.map((item) => item.querySelector('.badge')?.textContent?.trim() ?? null),
    );
  }

  /** Whether Needs attention sits beside Latest articles rather than below it. */
  async sideBySide(): Promise<boolean> {
    const latest = await this.latest.boundingBox();
    const attention = await this.attention.boundingBox();
    if (!latest || !attention) throw new Error('A section of Overview is not shown');
    return attention.x >= latest.x + latest.width && Math.abs(attention.y - latest.y) < 1;
  }

  /** Marks the document, so a later check can tell whether it was loaded again. */
  async markDocument(): Promise<void> {
    await this.page.evaluate(() => document.documentElement.setAttribute('data-e2e-kept', ''));
  }

  documentWasKept(): Promise<boolean> {
    return this.page.evaluate(() => document.documentElement.hasAttribute('data-e2e-kept'));
  }

  async open(): Promise<void> {
    await this.page.goto('/');
  }
}
