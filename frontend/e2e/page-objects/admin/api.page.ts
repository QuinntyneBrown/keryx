import type { Locator, Page } from '@playwright/test';

/**
 * API (docs/mocks/pages/api/). Journey 7 builds its header with "New API key", the key-created
 * callout, the empty and error states, and a table of each client's product, key and scopes with
 * Revoke; journey 21 adds the usage summary, the usage columns and the endpoints with their example.
 */
export class ApiPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly newKey: Locator;
  readonly navLink: Locator;
  readonly callout: Locator;
  readonly calloutTitle: Locator;
  readonly key: Locator;
  readonly copy: Locator;
  readonly empty: Locator;
  readonly emptyTitle: Locator;
  readonly emptyText: Locator;
  readonly emptyAction: Locator;
  readonly emptyReference: Locator;
  readonly tryAgain: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly products: Locator;
  readonly columns: Locator;
  readonly subLines: Locator;
  readonly usage: Locator;
  readonly baseUrlCaption: Locator;
  readonly baseUrl: Locator;
  readonly statLabels: Locator;
  readonly statValues: Locator;
  readonly endpointsHeading: Locator;
  readonly endpointList: Locator;
  readonly endpointMethods: Locator;
  readonly currentEndpoint: Locator;
  readonly examplePanel: Locator;
  readonly exampleTitle: Locator;
  readonly exampleStatus: Locator;
  readonly exampleRequest: Locator;
  readonly exampleResponse: Locator;
  readonly toast: Locator;
  readonly toastTitle: Locator;
  readonly toastText: Locator;
  readonly toastAction: Locator;

  constructor(private readonly page: Page) {
    const header = page.locator('main header.page-header');
    this.heading = header.locator('h1');
    this.description = header.locator('.page-header__description');
    this.newKey = header.locator('.page-header__actions .btn');
    this.navLink = page
      .getByRole('navigation', { name: 'Primary' })
      .locator('a.nav-link', { hasText: 'API' });
    this.callout = page.locator('main .callout');
    this.calloutTitle = this.callout.locator('.callout__title');
    this.key = this.callout.locator('.copy-field__value');
    this.copy = this.callout.locator('.copy-field .btn');
    this.empty = page.locator('main .empty');
    this.emptyTitle = this.empty.locator('.empty__title');
    this.emptyText = this.empty.locator('.empty__text');
    this.emptyAction = this.empty.locator('.empty__actions .btn');
    this.emptyReference = this.empty.locator('.empty__ref');
    this.tryAgain = this.empty.locator('.empty__actions .btn', { hasText: 'Try again' });
    this.table = page.locator('main table.table');
    this.rows = this.table.locator('tbody tr');
    this.products = this.rows.locator('.table__title');
    this.columns = this.table.locator('thead th');
    this.subLines = this.rows.locator('.table__sub');
    this.usage = page.locator('main section.summary-card');
    this.baseUrlCaption = this.usage.locator('.summary-card__lead .text-caption');
    this.baseUrl = this.usage.locator('.summary-card__value');
    this.statLabels = this.usage.locator('.stat__label');
    this.statValues = this.usage.locator('.stat__value');
    const endpoints = page.locator('main section[aria-labelledby="endpoints-title"]');
    this.endpointsHeading = endpoints.locator('h2');
    this.endpointList = endpoints.locator('ul.list');
    this.endpointMethods = endpoints.locator('.endpoint__method');
    this.currentEndpoint = endpoints.locator('.list__item[aria-current="true"]');
    this.examplePanel = endpoints.locator('.code-panel');
    this.exampleTitle = this.examplePanel.locator('.code-panel__head > :first-child');
    this.exampleStatus = this.examplePanel.locator('.code-panel__status');
    this.exampleRequest = this.examplePanel.locator('.code-panel__block').first();
    this.exampleResponse = this.examplePanel.locator('.code-panel__block').nth(1);
    const toast = page.locator('.toast-region .toast');
    this.toast = toast;
    this.toastTitle = toast.locator('.toast__title');
    this.toastText = toast.locator('.toast__text');
    this.toastAction = toast.locator('.toast__action');
  }

  /** Each row's "Requests · 24h", "Errors" and "Last call" cells, in order. */
  async usageCells(): Promise<string[][]> {
    return this.rows.evaluateAll((rows) =>
      rows.map((row) =>
        Array.from(row.querySelectorAll('td'))
          .slice(3, 6)
          .map((cell) => cell.textContent?.trim() ?? ''),
      ),
    );
  }

  /** Each endpoint's path, in order, without its method. */
  endpointPaths(): Promise<string[]> {
    return this.endpointList
      .locator('.endpoint__sig')
      .evaluateAll((sigs) => sigs.map((sig) => (sig.lastChild?.textContent ?? '').trim()));
  }

  /** The path of an endpoint row. */
  pathOf(row: Locator): Promise<string> {
    return row
      .locator('.endpoint__sig')
      .evaluate((sig) => (sig.lastChild?.textContent ?? '').trim());
  }

  /** The row of an endpoint, by its exact path. */
  endpoint(path: string): Locator {
    return this.endpointList.locator('.list__item').filter({
      has: this.page.locator('.endpoint__sig', {
        hasText: new RegExp(`GET\\s*${path.replace(/[{}]/g, '\\$&')}$`),
      }),
    });
  }

  async open(): Promise<void> {
    await this.page.goto('/api');
    await this.heading.waitFor();
  }

  /** The row of a product. */
  row(product: string): Locator {
    return this.rows.filter({ has: this.page.locator('.table__title', { hasText: product }) });
  }

  badge(product: string): Locator {
    return this.row(product).locator('.badge');
  }

  scopes(product: string): Locator {
    return this.row(product).locator('.tag');
  }

  revoke(product: string): Locator {
    return this.page.getByRole('button', { name: `Revoke the key for ${product}` });
  }

  /** Makes the browser refuse to copy, as when it blocks the clipboard. */
  async blockClipboard(): Promise<void> {
    await this.page.evaluate(() => {
      document.execCommand = () => false;
    });
  }

  /** Every place a key could be kept outside the screen: the URL and both storages. */
  async keptOutsideTheScreen(): Promise<string> {
    return this.page.evaluate(
      () =>
        location.href + JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }),
    );
  }

  /** The whole document's markup, to prove a key is gone from it. */
  markup(): Promise<string> {
    return this.page.content();
  }
}
