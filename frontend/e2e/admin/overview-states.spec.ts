// Acceptance Test
// Traces to: L2-046 AC2, AC3; L2-043 AC4; L2-073 AC2, AC3
// Description: Overview before anything is collected, after a failed run, and when it can't be read.

import { expect, test } from '../fixtures/test';
import {
  issuePage,
  LAST_14_RUNS,
  latestPage,
  OVERVIEW_NOW,
  overviewRun,
  overviewStatus,
  runPage,
} from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

const FAILURE = { status: 503, json: { status: 503, trace_id: 'req_8f2c41d0' } };

test.describe('Overview states', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  // L2-046 AC2: no source and no run.
  test('says nothing has been collected yet', async ({ page, api }) => {
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({ last_run: null, has_sources: false, has_runs: false }),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.pageStateTitle).toHaveText('Nothing has been collected yet');
    await expect(overview.pageStateText).toHaveText(
      'Keryx checks every source on a schedule — every 6 hours, Toronto time — and lists what it finds here. Add the first source to get started.',
    );
    await expect(overview.pageStateActions).toHaveText(['Add a source', 'Review the schedule']);
    await expect(overview.pageStateActions.nth(0)).toHaveAttribute('href', '/sources');
    await expect(overview.pageStateActions.nth(1)).toHaveAttribute('href', '/settings');
    await expect(overview.statusCard).toHaveCount(0);
    await expect(overview.latest).toHaveCount(0);
  });

  // L2-046 AC2: a source with no run yet, or runs whose sources were all removed, is not empty.
  for (const [has_sources, has_runs] of [
    [true, false],
    [false, true],
  ] as const) {
    test(`is not empty with sources ${has_sources} and runs ${has_runs}`, async ({ page, api }) => {
      api.on('GET', '/admin/v1/overview/status', () => ({
        json: overviewStatus({ last_run: null, has_sources, has_runs }),
      }));
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.latestItems).toHaveCount(7);
      await expect(overview.pageState).toHaveCount(0);
    });
  }

  // L2-043 AC4: after a failed run, the articles from earlier runs are still served.
  test('notes that the API still serves earlier articles after a failed run', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({
        last_run: overviewRun({ outcome: 'failed', failure_code: 'store_failed', added: 0 }),
      }),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.headline).toHaveText('Run failed — database unreachable');
    await expect(overview.latestNotice.locator('.empty__title')).toHaveText(
      'No articles were collected in the last run',
    );
    await expect(overview.latestNotice.locator('.empty__text')).toHaveText(
      'The run stopped before anything was stored. The API is still serving the 18,432 articles from earlier runs.',
    );
    await expect(overview.latestItems).toHaveCount(0);
    await expect(overview.viewAll).toHaveText('View all 18,432');
  });

  // L2-046 AC3, L2-073 AC2 and AC3: every request fails; Try again repeats them all.
  test('shows the page error when nothing loads, and recovers on Try again', async ({
    page,
    api,
  }) => {
    let fail = true;
    let statusAsks = 0;
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (answer = resolve));
    api.on('GET', '/admin/v1/runs', () => (fail ? FAILURE : { json: runPage(LAST_14_RUNS) }));
    api.on('GET', '/admin/v1/articles', () => (fail ? FAILURE : { json: latestPage() }));
    api.on('GET', '/admin/v1/issues', () => (fail ? FAILURE : { json: issuePage() }));
    api.on('GET', '/admin/v1/overview/status', async () => {
      statusAsks++;
      if (fail) return FAILURE;
      await answered;
      return { json: overviewStatus() };
    });
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.pageStateTitle).toHaveText('The overview didn’t load');
    await expect(overview.pageStateText).toHaveText(
      "Keryx couldn't reach its API just now. Collection keeps running on schedule; only this screen is affected. Try again, and if it keeps failing, send the reference below to whoever runs Keryx.",
    );
    await expect(overview.pageStateReference).toHaveText('Reference req_8f2c41d0');
    await expect(overview.pageStateActions).toHaveText(['Try again']);
    await expect(overview.statusCard).toHaveCount(0);

    fail = false;
    await overview.pageStateActions.first().click();

    await expect(overview.pageStateActions.first()).toHaveAttribute('aria-busy', 'true');
    answer();

    await expect(overview.headline).toHaveText('Completed with 2 failing sources');
    await expect(overview.latestItems).toHaveCount(7);
    await expect(overview.issueCards).toHaveCount(3);
    await expect(overview.bars).toHaveCount(14);
    await expect(overview.pageState).toHaveCount(0);
    expect(statusAsks).toBe(2);
  });
});
