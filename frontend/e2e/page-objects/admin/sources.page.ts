import type { Locator, Page } from '@playwright/test';

/**
 * Sources (docs/mocks/pages/sources/). Journey 3 builds its header, its empty and error states
 * and a table of each source's name, address, health and region; the kind chips, yield, last
 * fetch, row menu and paging arrive with journey 17 (ADR 0008).
 */
export class SourcesPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly addSource: Locator;
  readonly navLink: Locator;
  readonly empty: Locator;
  readonly emptyTitle: Locator;
  readonly emptyText: Locator;
  readonly emptyAction: Locator;
  readonly emptyReference: Locator;
  readonly tryAgain: Locator;
  readonly goToOverview: Locator;
  readonly table: Locator;
  readonly caption: Locator;
  readonly columns: Locator;
  readonly rows: Locator;

  constructor(private readonly page: Page) {
    const header = page.locator('main header.page-header');
    this.heading = header.locator('h1');
    this.description = header.locator('.page-header__description');
    this.addSource = header.locator('.page-header__actions .btn');
    this.navLink = page
      .getByRole('navigation', { name: 'Primary' })
      .locator('a.nav-link', { hasText: 'Sources' });
    this.empty = page.locator('main .empty');
    this.emptyTitle = this.empty.locator('.empty__title');
    this.emptyText = this.empty.locator('.empty__text');
    this.emptyAction = this.empty.locator('.empty__actions .btn');
    this.emptyReference = this.empty.locator('.empty__ref');
    this.tryAgain = this.empty.locator('.empty__actions .btn', { hasText: 'Try again' });
    this.goToOverview = this.empty.locator('.empty__actions .btn', { hasText: 'Go to overview' });
    this.table = page.locator('main table.table');
    this.caption = this.table.locator('caption');
    this.columns = this.table.locator('thead th');
    this.rows = this.table.locator('tbody tr');
  }

  async open(): Promise<void> {
    await this.page.goto('/sources');
    await this.heading.waitFor();
  }

  /** The name, the address as read aloud, the health as read aloud and the region of a row. */
  row(index: number) {
    const row = this.rows.nth(index);
    return {
      name: row.locator('.source .table__title'),
      address: row.locator('.source .table__sub--mono'),
      health: row.locator('td.from-lg .health'),
      region: row.locator('td.table__muted').first(),
    };
  }

  path(): string {
    return new URL(this.page.url()).pathname;
  }
}
