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
  /** The kind chips: All, RSS, Website, Instagram, Eventbrite, News query (L2-062 AC4). */
  readonly chips: Locator;
  /** The page body, busy while the first answer is out (L2-073). */
  readonly body: Locator;
  readonly skeletonRows: Locator;
  readonly loadingStatus: Locator;
  private readonly kindGroup: Locator;
  /** "Showing 1–12 of 64 sources, failing first." under the table. */
  readonly range: Locator;
  readonly previous: Locator;
  readonly next: Locator;
  readonly toastRegion: Locator;
  readonly toast: Locator;
  readonly toastTitle: Locator;
  readonly toastText: Locator;
  readonly toastAction: Locator;
  readonly toastDismiss: Locator;

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
    this.body = page.locator('main .page-body');
    this.skeletonRows = this.rows.filter({ has: page.locator('.skeleton') });
    this.loadingStatus = page.getByRole('status').filter({ hasText: 'Loading sources…' });
    this.kindGroup = page.getByRole('group', { name: 'Filter by source type' });
    this.chips = this.kindGroup.locator('.filter-chip');
    const pagination = page.locator('main .pagination');
    this.range = page.locator('main .table-footer p');
    this.previous = pagination.getByRole('button', { name: 'Previous' });
    this.next = pagination.getByRole('button', { name: 'Next' });
    this.toastRegion = page.locator('.toast-region');
    this.toast = this.toastRegion.locator('.toast');
    this.toastTitle = this.toast.locator('.toast__title');
    this.toastText = this.toast.locator('.toast__text');
    this.toastAction = this.toast.locator('.toast__action');
    this.toastDismiss = this.toast.getByRole('button', { name: 'Dismiss' });
  }

  async open(query = ''): Promise<void> {
    await this.page.goto(`/sources${query}`);
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
      articles: row.locator('td.num'),
      lastFetched: row.locator('td.table__muted').nth(1),
    };
  }

  chip(label: string): Locator {
    return this.kindGroup.getByRole('button', { name: label, exact: true });
  }

  search(): string {
    return new URL(this.page.url()).search;
  }

  path(): string {
    return new URL(this.page.url()).pathname;
  }
}
