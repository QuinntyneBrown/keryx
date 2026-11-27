import type { Locator, Page } from '@playwright/test';

/** Runs (docs/mocks/pages/runs/): the run history beside the chosen run's detail. */
export class RunsPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly navLink: Locator;
  readonly overviewLink: Locator;
  readonly history: Locator;
  readonly runs: Locator;
  readonly runTitles: Locator;
  readonly runMetas: Locator;
  readonly runWords: Locator;
  readonly runFlags: Locator;
  readonly current: Locator;
  readonly detail: Locator;
  readonly detailHeading: Locator;
  readonly detailBadge: Locator;
  readonly detailMeta: Locator;
  readonly tileLabels: Locator;
  readonly tileValues: Locator;

  constructor(private readonly page: Page) {
    const main = page.locator('main');
    this.heading = main.getByRole('heading', { level: 1 });
    this.description = main.locator('.page-header__description');
    this.navLink = page.getByRole('link', { name: 'Runs', exact: true });
    this.overviewLink = page.getByRole('link', { name: 'Overview', exact: true });
    this.history = main.getByRole('region', { name: 'Run history' });
    this.runs = this.history.locator('a.run-item');
    this.runTitles = this.runs.locator('.list__title');
    this.runMetas = this.runs.locator('.list__meta');
    this.runWords = this.runs.locator('.visually-hidden');
    this.runFlags = this.runs.locator('.run-item__flag');
    this.current = this.runs.and(page.locator('[aria-current="true"]'));
    this.detail = main.locator('section[aria-labelledby="run-title"]');
    this.detailHeading = this.detail.locator('#run-title');
    this.detailBadge = this.detail.locator('.card__title-row .badge');
    this.detailMeta = this.detail.locator('.card__meta');
    this.tileLabels = this.detail.locator('.stats .stat__label');
    this.tileValues = this.detail.locator('.stats .stat__value');
  }

  /** Each run's icon tone, in list order. */
  async runTones(): Promise<(string | null)[]> {
    return this.runs.evaluateAll((items) =>
      items.map((item) => item.querySelector('[data-tone]')?.getAttribute('data-tone') ?? null),
    );
  }

  /** The page's address after the path: its query string. */
  search(): string {
    return new URL(this.page.url()).search;
  }

  async open(query = ''): Promise<void> {
    await this.page.goto(`/runs${query}`);
  }
}
