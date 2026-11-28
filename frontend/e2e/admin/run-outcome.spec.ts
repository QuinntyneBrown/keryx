// Acceptance Test
// Traces to: L2-076 AC2, AC3, AC4; L2-058 AC3
// Description: When the run in progress ends, one notification per run says how it ended:
// finished, finished with failing sources (with View run), or failed (with Retry now).

import type { Page } from '@playwright/test';
import { expect, test, type ApiHandlers } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import {
  RUN_HISTORY,
  RUN_STARTED,
  RUNNING_RUN,
  runDetail,
  runIssue,
  runningDetail,
  stage,
} from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

/**
 * run_2419 is in progress until `end()`, then reads as `ended` everywhere: its detail, the runs
 * list and Overview's status.
 */
function runThatEnds(api: ApiHandlers, ended: Record<string, unknown>): () => void {
  let over = false;
  const summary = () => (over ? { ...RUNNING_RUN, ...ended } : RUNNING_RUN);
  api.on('GET', '/admin/v1/runs', () => ({
    json: { items: [summary(), ...RUN_HISTORY.slice(0, 13)], total: 15, page: 1, page_size: 14 },
  }));
  api.on('GET', '/admin/v1/runs/run_2419', () => ({
    json: over ? runDetail('run_2419', ended) : runningDetail(),
  }));
  api.on('GET', '/admin/v1/overview/status', () => ({
    json: overviewStatus(
      over
        ? { last_run: overviewRun({ id: 'run_2419', outcome: ended['outcome'] }) }
        : {
            current_run: overviewRun({
              id: 'run_2419',
              started_at: '2026-11-28T14:46:00Z',
              finished_at: null,
              duration_ms: null,
              outcome: 'running',
            }),
          },
    ),
  }));
  return () => (over = true);
}

/** Steps the clock until a notification shows. */
async function untilNotified(page: Page, title: import('@playwright/test').Locator): Promise<void> {
  await expect(async () => {
    await page.clock.runFor(500);
    await expect(title).toHaveCount(1, { timeout: 50 });
  }).toPass({ intervals: [0], timeout: 10_000 });
}

test.describe('Collection outcome', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
  });

  test('says the collection finished, once', async ({ page, api }) => {
    const end = runThatEnds(api, {
      outcome: 'succeeded',
      finished_at: '2026-11-28T14:50:12Z',
      added: 47,
      sources_failed: 0,
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.runNow).toHaveText('Collecting…');

    end();
    await untilNotified(page, runs.toastTitle);

    await expect(runs.toastTitle).toHaveText('Collection finished');
    await expect(runs.toastText).toHaveText('47 new articles from 64 sources.');
    await expect(runs.toastAction).toHaveText('View run');
    await expect(runs.toast).toHaveClass(/toast--success/);
    // Every read after the end, by the list or the detail, leaves it the only one.
    await page.clock.runFor(6000);
    await expect(runs.toast).toHaveCount(1);
  });

  test('names the failing sources and opens the run', async ({ page, api }) => {
    const end = runThatEnds(api, {
      outcome: 'completed_with_issues',
      finished_at: '2026-11-28T14:50:12Z',
      added: 47,
      sources_failed: 2,
      issues: [
        runIssue('iss_1', 'error', 'source_timeout', 'Redeemed Run Club Toronto', 'Timed out'),
        runIssue('iss_2', 'error', 'source_not_found', 'Covenant Cycling Collective', 'Not found'),
      ],
    });
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.eyebrow).toHaveText('Current run');

    end();
    await untilNotified(page, overview.toastTitle);

    await expect(overview.toastTitle).toHaveText('Collection finished with 2 failing sources');
    await expect(overview.toastText).toHaveText(
      '47 new articles. Redeemed Run Club Toronto and Covenant Cycling Collective didn’t respond.',
    );
    await expect(overview.toast).toHaveClass(/toast--warning/);

    await overview.toastAction.click();

    const runs = new RunsPage(page);
    await expect.poll(() => runs.search()).toBe('?run=run_2419');
    await expect(runs.toast).toHaveCount(0);
  });

  test('says the collection failed, and starts another on Retry now', async ({ page, api }) => {
    let posts = 0;
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('POST', '/admin/v1/runs', () => {
      posts += 1;
      return { status: 202, json: { ...RUN_STARTED, id: 'run_2420' } };
    });
    const end = runThatEnds(api, {
      outcome: 'failed',
      finished_at: '2026-11-28T14:50:12Z',
      added: 0,
      stages: [stage('fetch', 'done', 160, {}), stage('store', 'failed', 0, {})],
    });
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.eyebrow).toHaveText('Current run');

    end();
    await untilNotified(page, overview.toastTitle);

    await expect(overview.toastTitle).toHaveText('Collection failed');
    await expect(overview.toastText).toHaveText(
      'Keryx couldn’t reach its database, so nothing was stored. The API is still serving the previous run.',
    );
    await expect(overview.toast).toHaveClass(/toast--danger/);

    await overview.toastAction.click();

    await expect(overview.toastTitle).toHaveText('Collection started');
    await expect(overview.runNow).toHaveText('Collecting…');
    expect(posts).toBe(1);
  });
});
