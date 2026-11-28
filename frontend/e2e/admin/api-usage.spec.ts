// Acceptance Test
// Traces to: L2-037; L2-067 AC1, AC2; L2-090
// Description: The API screen shows the base address and the last 24 hours' requests, p95
// latency and error rate above the table, and each client's requests, error rate and last call
// in its row, and in its sub-line below 1024 px. With no requests the tiles read 0, — and —.

import { expect, test } from '../fixtures/test';
import { NO_USAGE, pageOf, USAGE, USAGE_CLIENTS } from '../fixtures/api';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { ApiPage } from '../page-objects/admin/api.page';

test.describe('API usage', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf(USAGE_CLIENTS) }));
    api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  });

  test('shows the base address and the last 24 hours’ figures', async ({ page }) => {
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.usage).toHaveAttribute('aria-label', 'API usage');
    await expect(screen.baseUrlCaption).toHaveText('Base URL');
    await expect(screen.baseUrl).toHaveText('https://api.keryx.ca/v1');
    await expect(screen.statLabels).toHaveText(['Requests · 24h', 'p95 latency', 'Error rate']);
    await expect(screen.statValues).toHaveText(['52,826', '84ms', '0.05%']);
    await expect(screen.statValues.nth(1).locator('.stat__unit')).toHaveText('ms');
  });

  test('shows each client’s requests, error rate and last call', async ({ page }) => {
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.columns).toHaveText([
      'Product',
      'Key',
      'Scopes',
      'Requests · 24h',
      'Errors',
      'Last call',
      'Actions',
    ]);
    expect(await screen.usageCells()).toEqual([
      ['48,210', '0.02%', 'just now'],
      ['96', '0%', '6:15 AM'],
      ['3,402', '0.4%', '2 min ago'],
      ['1,118', '0%', '11 min ago'],
      ['0', '—', 'Feb 2'],
    ]);
    await expect(screen.subLines).toHaveText([
      'kx_live_7Hq2… · 48,210 requests · just now',
      'kx_live_c91L… · 96 requests · 6:15 AM',
      'kx_live_Pz0e… · 3,402 requests · 2 min ago',
      'kx_live_4mWb… · 1,118 requests · 11 min ago',
      'kx_test_aa03… · 0 requests · Feb 2',
    ]);
  });

  test('reads 0, — and — with no requests, and never for a client that has not called', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/api-usage', () => ({ json: NO_USAGE }));
    api.on('GET', '/admin/v1/api-clients', () => ({
      json: pageOf([USAGE_CLIENTS[4]].map((c) => ({ ...c, last_call_at: null }))),
    }));
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.statValues).toHaveText(['0', '—', '—']);
    expect(await screen.usageCells()).toEqual([['0', '—', 'never']]);
  });

  test('shows the figures above the empty state when no product is connected', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/api-usage', () => ({ json: NO_USAGE }));
    api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf([]) }));
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.emptyTitle).toHaveText('No products are connected yet');
    await expect(screen.baseUrl).toHaveText('https://api.keryx.ca/v1');
    await expect(screen.statValues).toHaveText(['0', '—', '—']);
  });
});
