// Acceptance Test
// Traces to: L2-067 AC4, AC5, AC6, AC9
// Description: The API screen lists every consumer endpoint of L2-026 to L2-030. The first is
// current to begin with; choosing another, by pointer or by keyboard, marks it current and shows
// its curl request, with the placeholder key and the configured base address, and a sample
// 200 OK response. The section stays when no product is connected, and from 1024 px the list
// and the example sit side by side.

import { expect, test } from '../fixtures/test';
import { pageOf, USAGE, USAGE_CLIENTS } from '../fixtures/api';
import { ApiPage } from '../page-objects/admin/api.page';

const PATHS = [
  '/v1/articles',
  '/v1/articles/{id}',
  '/v1/events',
  '/v1/events/{id}',
  '/v1/organizations',
  '/v1/organizations/{id}',
  '/v1/sources',
  '/v1/categories',
  '/v1/regions',
  '/v1/status',
];

const header = '  -H "Authorization: Bearer kx_live_…"';

test.describe('API endpoints', () => {
  test.beforeEach(({ api }) => {
    api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf(USAGE_CLIENTS) }));
    api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  });

  test('lists every endpoint, the first current with its example', async ({ page }) => {
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.endpointsHeading).toHaveText('Endpoints');
    await expect(screen.endpointMethods).toHaveText(PATHS.map(() => 'GET'));
    expect(await screen.endpointPaths()).toEqual(PATHS);
    await expect(screen.currentEndpoint).toHaveCount(1);
    await expect.poll(() => screen.pathOf(screen.currentEndpoint)).toBe('/v1/articles');
    await expect(screen.currentEndpoint.locator('.endpoint__desc')).toHaveText(
      'Published articles, newest first. Filter by region, category, org, since.',
    );
    await expect(screen.exampleTitle).toHaveText('Example request');
    await expect(screen.exampleStatus).toHaveText('200 OK');
    await expect(screen.exampleRequest).toHaveText(
      `curl https://api.keryx.ca/v1/articles?region=toronto&limit=2 \\\n${header}`,
    );
    await expect(screen.exampleRequest).toHaveAttribute('aria-label', 'Example request');
    await expect(screen.exampleResponse).toHaveAttribute('aria-label', 'Example response');
    const response = JSON.parse((await screen.exampleResponse.textContent()) ?? '');
    expect(Object.keys(response)).toEqual(['data', 'next_cursor']);
  });

  test('choosing an endpoint makes it current and shows its example', async ({ page }) => {
    const screen = new ApiPage(page);
    await screen.open();

    await screen.endpoint('/v1/status').click();

    await expect.poll(() => screen.pathOf(screen.currentEndpoint)).toBe('/v1/status');
    await expect(screen.currentEndpoint).toHaveCount(1);
    await expect(screen.exampleRequest).toHaveText(
      `curl https://api.keryx.ca/v1/status \\\n${header}`,
    );
    const response = JSON.parse((await screen.exampleResponse.textContent()) ?? '');
    expect(Object.keys(response)).toEqual([
      'last_run_at',
      'status',
      'next_run_at',
      'articles_total',
    ]);
  });

  test('an endpoint is chosen by keyboard, and its code can be reached to scroll', async ({
    page,
  }) => {
    const screen = new ApiPage(page);
    await screen.open();

    await screen.endpoint('/v1/events/{id}').focus();
    await page.keyboard.press('Enter');

    await expect.poll(() => screen.pathOf(screen.currentEndpoint)).toBe('/v1/events/{id}');
    await expect(screen.exampleRequest).toHaveText(
      `curl https://api.keryx.ca/v1/events/evt_7k2m9q4x8b1n \\\n${header}`,
    );
    // The last endpoint is followed by the code, each block focusable so it scrolls by keyboard.
    await screen.endpoint('/v1/status').focus();
    await page.keyboard.press('Tab');
    await expect(screen.exampleRequest).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(screen.exampleResponse).toBeFocused();
  });

  test('no example carries a real key', async ({ page }) => {
    const screen = new ApiPage(page);
    await screen.open();

    for (const path of PATHS) {
      await screen.endpoint(path).click();
      await expect(screen.exampleRequest).toContainText('Bearer kx_live_…"');
      expect(await screen.examplePanel.textContent()).not.toMatch(/kx_(live|test)_[A-Za-z0-9]/);
    }
  });

  test('the endpoints are still shown when no product is connected', async ({ page, api }) => {
    api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf([]) }));
    const screen = new ApiPage(page);
    await screen.open();

    await expect(screen.emptyTitle).toHaveText('No products are connected yet');
    await expect(screen.endpointMethods).toHaveCount(PATHS.length);
    await expect(screen.exampleRequest).toContainText('/v1/articles');
  });

  test('the example sits under the list below 1024 px and beside it from 1024 px', async ({
    page,
  }) => {
    const screen = new ApiPage(page);
    await page.setViewportSize({ width: 1023, height: 900 });
    await screen.open();
    const list = screen.endpointList;

    let [listBox, panelBox] = [await list.boundingBox(), await screen.examplePanel.boundingBox()];
    expect(panelBox!.y).toBeGreaterThanOrEqual(listBox!.y + listBox!.height);

    await page.setViewportSize({ width: 1024, height: 900 });
    await expect
      .poll(async () => (await screen.examplePanel.boundingBox())!.y)
      .toBe((await list.boundingBox())!.y);
    [listBox, panelBox] = [await list.boundingBox(), await screen.examplePanel.boundingBox()];
    expect(panelBox!.x).toBeGreaterThanOrEqual(listBox!.x + listBox!.width);
  });
});
