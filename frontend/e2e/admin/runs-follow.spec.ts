// Acceptance Test
// Traces to: L2-058 AC1, AC2
// Description: A run in progress is followed live: its detail is read again no more often than
// every 3 seconds, by one poll at a time on any screen, the counts on Runs advance as the server
// reports them, and the reads stop when the run ends.

import type { Page } from '@playwright/test';
import { expect, test, type ApiHandlers } from '../fixtures/test';
import { overviewRun, overviewStatus } from '../fixtures/overview';
import { RUN_HISTORY, RUNNING_NOW, RUNNING_RUN, runDetail, runningDetail } from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

/**
 * The answers run_2419's detail gives, one per read, the last repeated; the list and Overview's
 * status serve the run as its last answer left it.
 */
function followRun(api: ApiHandlers, answers: Record<string, unknown>[]): () => number {
  let reads = 0;
  const latest = () => answers[Math.max(0, Math.min(reads, answers.length) - 1)];
  api.on('GET', '/admin/v1/runs', () => ({
    json: {
      items: [{ ...RUNNING_RUN, outcome: latest()['outcome'] }, ...RUN_HISTORY.slice(0, 13)],
      total: 15,
      page: 1,
      page_size: 14,
    },
  }));
  api.on('GET', '/admin/v1/runs/run_2419', () => {
    reads += 1;
    return { json: latest() };
  });
  api.on('GET', '/admin/v1/overview/status', () => ({
    json: overviewStatus(
      latest()['outcome'] === 'running'
        ? {
            current_run: overviewRun({
              id: 'run_2419',
              started_at: '2026-11-28T14:46:00Z',
              finished_at: null,
              duration_ms: null,
              outcome: 'running',
            }),
          }
        : {},
    ),
  }));
  return () => reads;
}

/**
 * Steps the clock in quarter seconds until the next read of the run goes, and gives how long that
 * took. A second poll would show as a read sent sooner, or as two reads at once.
 */
async function untilNextRead(page: Page, reads: () => number): Promise<number> {
  const next = reads() + 1;
  let stepped = 0;
  await expect(async () => {
    await page.clock.runFor(250);
    stepped += 250;
    expect(reads()).toBeGreaterThanOrEqual(next);
  }).toPass({ intervals: [0], timeout: 10_000 });
  expect(reads()).toBe(next);
  return stepped;
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

    const waited = await untilNextRead(page, reads);

    expect(waited).toBeGreaterThanOrEqual(3000);
    expect(waited).toBeLessThanOrEqual(3500);
    await expect(runs.stepDetails.first()).toHaveText('52 of 64 sources checked');
    expect(await untilNextRead(page, reads)).toBeGreaterThanOrEqual(3000);
  });

  test('stops reading once the run has ended', async ({ page, api }) => {
    const reads = followRun(api, [
      runningDetail(),
      runDetail('run_2419', { outcome: 'succeeded', finished_at: '2026-11-28T14:50:12Z' }),
    ]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');
    await untilNextRead(page, reads);
    await expect(runs.detailBadge).not.toHaveText('Running');

    await page.clock.runFor(30_000);
    // A read the timer had sent would reach the server before one sent after it: opening another
    // run proves the page is still answering.
    await runs.runs.nth(1).click();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    expect(reads()).toBe(2);
  });

  test('keeps the run chosen when the one in progress is read', async ({ page, api }) => {
    const reads = followRun(api, [runningDetail()]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');

    await runs.runs.nth(1).click();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await untilNextRead(page, reads);
    await page.clock.runFor(500);

    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
  });

  test('leaves one poll, no faster, once Runs is left', async ({ page, api }) => {
    const reads = followRun(api, [runningDetail()]);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.stepDetails.first()).toHaveText('38 of 64 sources checked');

    await runs.overviewLink.click();
    await expect(new OverviewPage(page).eyebrow).toBeVisible();

    expect(await untilNextRead(page, reads)).toBeGreaterThanOrEqual(2750);
    expect(await untilNextRead(page, reads)).toBeGreaterThanOrEqual(3000);
  });
});
