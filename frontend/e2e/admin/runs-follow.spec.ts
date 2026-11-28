// Acceptance Test
// Traces to: L2-058 AC1, AC2
// Description: Runs follows a run in progress: it reads the run's detail again no more often than
// every 3 seconds, the counts advance as the server reports them, and it stops reading when the
// run ends or the screen is left.

import { expect, test, type ApiHandlers } from '../fixtures/test';
import { RUN_HISTORY, RUNNING_NOW, RUNNING_RUN, runDetail, runningDetail } from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

/** The answers run_2419's detail gives, one per read, the last repeated. */
function followRun(api: ApiHandlers, answers: object[]): () => number {
  let reads = 0;
  api.on('GET', '/admin/v1/runs', () => ({
    json: { items: [RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)], total: 15, page: 1, page_size: 14 },
  }));
  api.on('GET', '/admin/v1/runs/run_2419', () => {
    reads += 1;
    return { json: answers[Math.min(reads, answers.length) - 1] };
  });
  return () => reads;
}

test.describe('Following a running run', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: RUNNING_NOW });
  });

  test('reads the run again after 3 seconds, and the counts advance', async ({ page, api }) => {
    const reads = followRun(api, [
      runningDetail(),
      runningDetail({ sources_ok: 52, sources_checked: 52 }),
    ]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');
    expect(reads()).toBe(1);

    // Step the clock in quarter seconds until the second read goes.
    let stepped = 0;
    await expect(async () => {
      await page.clock.runFor(250);
      stepped += 250;
      expect(reads()).toBe(2);
    }).toPass({ intervals: [0], timeout: 10_000 });

    expect(stepped).toBeGreaterThanOrEqual(3000);
    expect(stepped).toBeLessThanOrEqual(3500);
    await expect(runs.stepDetails.first()).toHaveText('52 of 64 sources checked');
  });

  test('stops reading once the run has ended', async ({ page, api }) => {
    const reads = followRun(api, [
      runningDetail(),
      runDetail('run_2419', { outcome: 'succeeded', finished_at: '2026-11-28T14:50:12Z' }),
    ]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');
    await expect(async () => {
      await page.clock.runFor(1000);
      expect(reads()).toBe(2);
    }).toPass({ intervals: [0], timeout: 10_000 });
    await expect(runs.detailBadge).not.toHaveText('Running');

    await page.clock.runFor(30_000);
    // A read the timer had sent would reach the server before one sent after it: opening another
    // run proves the page is still answering.
    await runs.runs.nth(1).click();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    expect(reads()).toBe(2);
  });

  test('stops reading once another run is chosen', async ({ page, api }) => {
    const reads = followRun(api, [runningDetail()]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');

    await runs.runs.nth(1).click();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await page.clock.runFor(30_000);
    await runs.runs.nth(2).click();
    await expect(runs.detailHeading).not.toHaveText('Today, 6:00 AM');

    expect(reads()).toBe(1);
  });

  test('stops reading once the screen is left', async ({ page, api }) => {
    const reads = followRun(api, [runningDetail()]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');

    await runs.overviewLink.click();
    const overview = new OverviewPage(page);
    await expect(overview.heading).toHaveText('Overview');
    await page.clock.runFor(30_000);
    await expect(overview.eyebrow).toBeVisible();

    expect(reads()).toBe(1);
  });
});
