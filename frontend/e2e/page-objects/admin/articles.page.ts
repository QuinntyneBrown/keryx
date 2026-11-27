import type { Locator, Page } from '@playwright/test';

/**
 * Articles (docs/mocks/pages/articles/): the status tabs with their counts, the table of one
 * server page, newest collected first, and the range and pager under it. The locators are the
 * design system's BEM classes, which the mocks and the components library share.
 */
export class ArticlesPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly navLink: Locator;
  readonly tabList: Locator;
  readonly tabs: Locator;
  readonly panel: Locator;
  readonly table: Locator;
  readonly caption: Locator;
  readonly columns: Locator;
  readonly rows: Locator;
  readonly range: Locator;
  readonly pagination: Locator;
  readonly previous: Locator;
  readonly next: Locator;
  readonly search: Locator;
  readonly region: Locator;
  readonly category: Locator;
  readonly empty: Locator;
  readonly emptyTitle: Locator;
  readonly emptyText: Locator;
  readonly clearSearch: Locator;
  readonly detail: Locator;
  readonly panelId: Locator;
  readonly panelClose: Locator;
  readonly panelStatus: Locator;
  readonly panelTitle: Locator;
  readonly panelSummary: Locator;
  readonly panelLink: Locator;
  readonly panelTerms: Locator;
  readonly panelValues: Locator;
  readonly panelTags: Locator;
  readonly panelAlert: Locator;
  readonly toast: Locator;
  /** Below 1024 px the panel opens in a modal dialog over the screen. */
  readonly dialog: Locator;

  constructor(private readonly page: Page) {
    const header = page.locator('main header.page-header');
    this.heading = header.locator('h1');
    this.description = header.locator('.page-header__description');
    this.navLink = page
      .getByRole('navigation', { name: 'Primary' })
      .locator('a.nav-link', { hasText: 'Articles' });
    this.tabList = page.locator('main .tabs[role="tablist"]');
    this.tabs = this.tabList.locator('.tab');
    this.panel = page.locator('main [role="tabpanel"]');
    this.table = page.locator('main table.table');
    this.caption = this.table.locator('caption');
    this.columns = this.table.locator('thead th');
    this.rows = this.table.locator('tbody tr');
    this.range = page.locator('main .table-footer p');
    this.pagination = page.locator('main nav.pagination');
    this.previous = this.pagination.locator('.btn', { hasText: 'Previous' });
    this.next = this.pagination.locator('.btn', { hasText: 'Next' });
    const toolbar = page.locator('main .toolbar');
    this.search = toolbar.locator('.search input[type="search"]');
    this.region = toolbar.locator('select#filter-region');
    this.category = toolbar.locator('select#filter-category');
    this.empty = this.panel.locator('.empty');
    this.emptyTitle = this.empty.locator('.empty__title');
    this.emptyText = this.empty.locator('.empty__text');
    this.clearSearch = this.empty.locator('.empty__actions .btn');
    this.detail = page.locator('aside.detail-panel');
    this.panelId = this.detail.locator('.detail-panel__head .mono');
    this.panelClose = this.detail.locator('.detail-panel__head .icon-btn');
    this.panelStatus = this.detail.locator('.detail-panel__body .badge').first();
    this.panelTitle = this.detail.locator('.detail-panel__title');
    this.panelSummary = this.detail.locator('.detail-panel__summary');
    this.panelLink = this.detail.locator('.detail-panel__link');
    this.panelTerms = this.detail.locator('.dl dt');
    this.panelValues = this.detail.locator('.dl dd');
    this.panelTags = this.detail.locator('.dl dd .badge');
    this.panelAlert = this.detail.locator('.alert');
    this.toast = page.locator('.toast-region .toast');
    this.dialog = page.getByRole('dialog');
  }

  async open(query = ''): Promise<void> {
    await this.page.goto(`/articles${query}`);
    await this.heading.waitFor();
  }

  tab(name: string): Locator {
    return this.tabList.getByRole('tab', { name });
  }

  /** The cells of a row: its title, "organization · age", category, region, relevance and status. */
  row(index: number) {
    const row = this.rows.nth(index);
    return {
      title: row.locator('.table__title'),
      sub: row.locator('.table__sub').first(),
      category: row.locator('td').nth(1),
      region: row.locator('td').nth(2),
      relevance: row.locator('td.from-lg .meter__value'),
      meter: row.locator('td.from-lg .meter'),
      fill: row.locator('td.from-lg .meter__fill'),
      status: row.locator('td.from-lg .badge'),
    };
  }

  /**
   * Sends each key to whatever has focus, all in one task, so no navigation completes between
   * them: as when keys are pressed faster than the list loads.
   */
  async pressKeysAtOnce(keys: readonly string[]): Promise<void> {
    await this.page.evaluate((all) => {
      for (const key of all) {
        document.activeElement?.dispatchEvent(
          new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }),
        );
      }
    }, keys);
  }

  query(): URLSearchParams {
    return new URL(this.page.url()).searchParams;
  }
}
