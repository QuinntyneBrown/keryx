// Acceptance Test
// Traces to: L2-045 AC5; L2-046 AC3; L2-073 AC3
// Description: When one part of Overview fails, only that part says so and offers Try again; the
// rest of the page stays usable, and Try again repeats that part's request alone.

import { expect, test, type ApiHandlers, type ApiReply } from '../fixtures/test';
import {
  issuePage,
  LAST_14_RUNS,
  latestPage,
  OVERVIEW_NOW,
  OVERVIEW_REGIONS,
  overviewStatus,
  runPage,
} from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

const FAILURE: ApiReply = { status: 503, json: { status: 503, trace_id: 'req_5d0e7a12' } };

/** Counts each Overview request; `failing` lists the paths that fail until it is emptied. */
function answer(api: ApiHandlers, failing: Set<string>) {
  const asks: Record<string, number> = {};
  const on = (path: string, reply: () => ApiReply) =>
    api.on('GET', path, () => {
      asks[path] = (asks[path] ?? 0) + 1;
      return failing.has(path) ? FAILURE : reply();
    });
  on('/admin/v1/overview/status', () => ({ json: overviewStatus() }));
  on('/admin/v1/runs', () => ({ json: runPage(LAST_14_RUNS) }));
  on('/admin/v1/articles', () => ({ json: latestPage() }));
  on('/admin/v1/issues', () => ({ json: issuePage() }));
  return asks;
}

test.describe('Overview partial failure', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test(
    'shows Latest articles failing alone, and retries only it',
    { tag: '@critical' },
    async ({ page, api }) => {
      const failing = new Set(['/admin/v1/articles']);
      const asks = answer(api, failing);
      let release: () => void = () => undefined;
      const held = new Promise<void>((resolve) => (release = resolve));
      api.on('GET', '/admin/v1/regions', async () => {
        if (!failing.size) await held;
        return { json: { items: OVERVIEW_REGIONS } };
      });
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.latestError.locator('.empty__title')).toHaveText(
        "Latest articles didn't load",
      );
      await expect(overview.latestError.locator('.empty__text')).toHaveText(
        'The run status and issues are up to date; only this list failed.',
      );
      await expect(overview.headline).toHaveText('Completed with 2 failing sources');
      await expect(overview.bars).toHaveCount(14);
      await expect(overview.issueCards).toHaveCount(3);
      await expect(overview.pageState).toHaveCount(0);

      failing.clear();
      await overview.latestError.locator('.btn').click();

      await expect(overview.latestError.locator('.btn')).toHaveAttribute('aria-busy', 'true');
      release();
      await expect(overview.latestItems).toHaveCount(7);
      await expect(overview.latestError).toHaveCount(0);
      expect(asks).toEqual({
        '/admin/v1/overview/status': 1,
        '/admin/v1/runs': 1,
        '/admin/v1/articles': 2,
        '/admin/v1/issues': 1,
      });
    },
  );

  test('shows Needs attention failing alone, and retries only it', async ({ page, api }) => {
    const failing = new Set(['/admin/v1/issues']);
    const asks = answer(api, failing);
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.attentionError.locator('.empty__title')).toHaveText(
      "Needs attention didn't load",
    );
    await expect(overview.attentionError.locator('.empty__text')).toHaveText(
      'The run status and latest articles are up to date; only this list failed.',
    );
    await expect(overview.openCount).toHaveCount(0);
    await expect(overview.latestItems).toHaveCount(7);

    failing.clear();
    await overview.attentionError.locator('.btn').click();

    await expect(overview.issueCards).toHaveCount(3);
    expect(asks['/admin/v1/issues']).toBe(2);
    expect(asks['/admin/v1/articles']).toBe(1);
  });

  // The issue times still read in Toronto's zone when the status, which names it, failed.
  test('shows the run status failing alone, and retries it with its chart', async ({
    page,
    api,
  }) => {
    const failing = new Set(['/admin/v1/overview/status']);
    const asks = answer(api, failing);
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.statusError.locator('.empty__title')).toHaveText(
      "The run status didn't load",
    );
    await expect(overview.statusError.locator('.empty__text')).toHaveText(
      'The latest articles and issues are up to date; only the run status failed.',
    );
    await expect(overview.statusCard).toHaveCount(0);
    await expect(overview.issueMetas).toHaveText([
      'Today, 6:01 AM',
      '4 runs in a row · since Mon, 12:02 PM',
      'Today, 6:03 AM',
    ]);

    failing.clear();
    await overview.statusError.locator('.btn').click();

    await expect(overview.headline).toHaveText('Completed with 2 failing sources');
    await expect(overview.bars).toHaveCount(14);
    expect(asks['/admin/v1/overview/status']).toBe(2);
    expect(asks['/admin/v1/runs']).toBe(2);
    expect(asks['/admin/v1/issues']).toBe(1);
  });

  test('shows the run chart failing alone inside the status card', async ({ page, api }) => {
    const failing = new Set(['/admin/v1/runs']);
    answer(api, failing);
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.headline).toHaveText('Completed with 2 failing sources');
    await expect(overview.chartError.locator('.empty__title')).toHaveText(
      "The run chart didn't load",
    );
    await expect(overview.bars).toHaveCount(0);

    failing.clear();
    await overview.chartError.locator('.btn').click();

    await expect(overview.bars).toHaveCount(14);
  });

  // L2-046 AC3: the page error is for every request failing; one still loading is not a failure.
  test('waits for every part before showing the page error', async ({ page, api }) => {
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/runs', () => FAILURE);
    api.on('GET', '/admin/v1/articles', () => FAILURE);
    api.on('GET', '/admin/v1/issues', () => FAILURE);
    api.on('GET', '/admin/v1/overview/status', async () => {
      await held;
      return { json: overviewStatus() };
    });
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.latestError).toHaveCount(1);
    await expect(overview.attentionError).toHaveCount(1);
    await expect(overview.pageState).toHaveCount(0);
    release();
    await expect(overview.headline).toHaveText('Completed with 2 failing sources');
    await expect(overview.chartError).toHaveCount(1);
    await expect(overview.pageState).toHaveCount(0);
  });
});
