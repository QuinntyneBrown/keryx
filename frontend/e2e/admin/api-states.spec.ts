// Acceptance Test
// Traces to: L2-067 AC7, AC8; L2-073 AC1
// Description: While the API screen's answers take over 300 ms it shows skeletons shaped as the
// tiles, the table and the endpoints under a busy body. When only the usage fails, the tiles
// give way to an inline error with a reference and "Try again", and the table stays usable.
// The clients come 50 to a page, with a pager only when there is a second page.

import { expect, test } from '../fixtures/test';
import { apiClient, pageOf, USAGE, USAGE_CLIENTS } from '../fixtures/api';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { ApiPage } from '../page-objects/admin/api.page';

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('shows skeletons while the answers are slow, then the screen', async ({ page, api }) => {
  let answer: () => void = () => undefined;
  const answered = new Promise<void>((resolve) => (answer = resolve));
  api.on('GET', '/admin/v1/api-clients', async () => {
    await answered;
    return { json: pageOf(USAGE_CLIENTS) };
  });
  api.on('GET', '/admin/v1/api-usage', async () => {
    await answered;
    return { json: USAGE };
  });
  const screen = new ApiPage(page);
  await screen.open();

  await expect(screen.body).toHaveAttribute('aria-busy', 'true');
  // After 300 ms with no answer (skeletonDelay, whose unit tests time it).
  await expect(screen.loadingStatus).toHaveText('Loading API usage…');
  await expect(screen.usage.locator('.skeleton')).toHaveCount(8);
  await expect(screen.skeletonRows).toHaveCount(5);
  await expect(screen.endpointSkeletons).toHaveCount(2);
  await expect(screen.newKey).toBeDisabled();

  answer();

  await expect(screen.statValues).toHaveText(['52,826', '84ms', '0.05%']);
  await expect(screen.rows).toHaveCount(USAGE_CLIENTS.length);
  await expect(screen.skeletonRows).toHaveCount(0);
  await expect(screen.endpointSkeletons).toHaveCount(0);
  await expect(screen.body).not.toHaveAttribute('aria-busy');
  await expect(screen.loadingStatus).toHaveCount(0);
  await expect(screen.newKey).toBeEnabled();
});

test('when only the usage fails, the tiles say so and try again, and the table stays usable', async ({
  page,
  api,
}) => {
  let fail = true;
  let usageCalls = 0;
  let clientCalls = 0;
  api.on('GET', '/admin/v1/api-clients', () => {
    clientCalls += 1;
    return { json: pageOf(USAGE_CLIENTS) };
  });
  api.on('GET', '/admin/v1/api-usage', () => {
    usageCalls += 1;
    return fail
      ? { status: 503, json: { code: 'internal', trace_id: 'req_3b9e01d4' } }
      : { json: USAGE };
  });
  const screen = new ApiPage(page);
  await screen.open();

  await expect(screen.usageAlert).toHaveAttribute('role', 'alert');
  await expect(screen.usageAlertTitle).toHaveText('Usage didn’t load');
  await expect(screen.usageAlertText).toHaveText(
    'The keys below are up to date; only the usage figures failed. Reference req_3b9e01d4',
  );
  await expect(screen.statValues).toHaveCount(0);
  await expect(screen.baseUrl).toHaveCount(0);
  await expect(screen.rows).toHaveCount(USAGE_CLIENTS.length);
  await expect(screen.revoke('Kingdom Strong website widget')).toBeEnabled();
  await expect(screen.exampleRequest).toContainText('curl /v1/articles?region=toronto&limit=2');

  fail = false;
  await screen.usageTryAgain.click();

  await expect(screen.statValues).toHaveText(['52,826', '84ms', '0.05%']);
  await expect(screen.usageAlert).toHaveCount(0);
  await expect(screen.exampleRequest).toContainText('curl https://api.keryx.ca/v1/articles');
  expect(usageCalls).toBe(2);
  expect(clientCalls).toBe(1);
});

test('the clients come 50 to a page, with a pager only when there is a second page', async ({
  page,
  api,
}) => {
  const first = Array.from({ length: 50 }, (_, index) =>
    apiClient(
      `cli_${String(index).padStart(12, '0')}`,
      `Product ${index + 1}`,
      `kx_live_${String(index).padStart(4, '0')}`,
      ['articles:read'],
    ),
  );
  const last = apiClient('cli_zzzzzzzzzzzz', 'Product 51', 'kx_live_zz51', ['articles:read']);
  const asked: string[] = [];
  api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  api.on('GET', '/admin/v1/api-clients', (request) => {
    const page = new URL(request.url()).searchParams.get('page') ?? '1';
    asked.push(page);
    return page === '2'
      ? { json: { ...pageOf([last], 51), page: 2 } }
      : { json: pageOf(first, 51) };
  });
  const screen = new ApiPage(page);
  await screen.open();

  await expect(screen.rows).toHaveCount(50);
  await expect(screen.range).toHaveText('Showing 1–50 of 51 keys');
  await expect(screen.previous).toBeDisabled();

  await screen.next.click();

  await expect(screen.products).toHaveText(['Product 51']);
  await expect(screen.range).toHaveText('Showing 51–51 of 51 keys');
  await expect(screen.next).toBeDisabled();
  await screen.previous.click();
  await expect(screen.rows).toHaveCount(50);
  expect(asked).toEqual(['1', '2', '1']);
});

test('one page of clients shows no pager', async ({ page, api }) => {
  api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf(USAGE_CLIENTS) }));
  const screen = new ApiPage(page);
  await screen.open();

  await expect(screen.rows).toHaveCount(USAGE_CLIENTS.length);
  await expect(screen.pager).toHaveCount(0);
});
