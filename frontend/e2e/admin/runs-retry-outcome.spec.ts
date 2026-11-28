// Acceptance Test
// Traces to: L2-059 AC2, AC3
// Description: When a retry of a run's failed sources ends, the run's tiles update without a
// reload and one notification says which sources responded and which still fail, with "Edit
// source" or "Edit sources", which open Sources (ADR 0012).

import type { Page } from '@playwright/test';
import { expect, test, type ApiHandlers } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, RUN_HISTORY, runDetail } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

type Phase = 'failed' | 'retrying' | 'ended';

/**
 * run_2418, with Redeemed Run Club (404) and Covenant Cycling (timed out) failed, through its
 * retry to an end in which `stillFailing` of its issues remain.
 */
function retryOf(api: ApiHandlers, stillFailing: Record<string, unknown>[]): (next: Phase) => void {
  let phase: Phase = 'failed';
  const outcome = () => (phase === 'retrying' ? 'running' : 'completed_with_issues');
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  api.on('POST', '/admin/v1/runs/run_2418/retry', () => {
    phase = 'retrying';
    return { status: 202, json: { id: 'run_2418' } };
  });
  api.on('GET', '/admin/v1/runs', () => ({
    json: {
      items: [{ ...RUN_HISTORY[0], outcome: outcome() }, ...RUN_HISTORY.slice(1)],
      total: 14,
      page: 1,
      page_size: 14,
    },
  }));
  api.on('GET', '/admin/v1/runs/run_2418', () => ({
    json:
      phase === 'failed'
        ? runDetail('run_2418', { issues: RUN_2418_ISSUES })
        : phase === 'retrying'
          ? runDetail('run_2418', { outcome: 'running', sources_checked: 0, sources_total: 2 })
          : runDetail('run_2418', {
              sources_ok: 64 - stillFailing.length,
              sources_failed: stillFailing.length,
              issues: [...stillFailing, RUN_2418_ISSUES[2]],
            }),
  }));
  api.on('GET', '/admin/v1/sources', () => ({
    json: {
      items: [],
      total: 0,
      page: 1,
      page_size: 12,
      all_count: 0,
      failing_count: 0,
      needs_look_count: 0,
    },
  }));
  return (next) => (phase = next);
}

/** Retries run_2418 and lets the "Retrying" notification leave. */
async function retry(page: Page, runs: RunsPage): Promise<void> {
  await runs.open('?run=run_2418');
  await runs.retry.click();
  await expect(runs.toastTitle).toHaveText('Retrying 2 failed sources');
  await page.clock.runFor(9000);
  await expect(runs.toast).toHaveCount(0);
}

/** Steps the clock until a notification shows. */
async function untilNotified(page: Page, runs: RunsPage): Promise<void> {
  await expect(async () => {
    await page.clock.runFor(1000);
    await expect(runs.toast).toHaveCount(1, { timeout: 50 });
  }).toPass({ intervals: [0], timeout: 10_000 });
}

test.describe('Retry outcome', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
  });

  test('says one of two recovered, and updates the tiles', async ({ page, api }) => {
    const move = retryOf(api, [RUN_2418_ISSUES[0]]);
    const runs = new RunsPage(page);
    await retry(page, runs);

    move('ended');
    await untilNotified(page, runs);

    await expect(runs.toastTitle).toHaveText('1 of 2 sources recovered');
    await expect(runs.toastText).toHaveText(
      'Covenant Cycling Collective responded. Redeemed Run Club Toronto still returns 404.',
    );
    await expect(runs.toast).toHaveClass(/toast--warning/);
    await expect(runs.tileValues).toHaveText(['47', '18', '63', '1']);
    await expect(runs.detailBadge).not.toHaveText('Running');

    await runs.toastAction.click();

    await expect(page).toHaveURL(/\/sources$/);
  });

  test('says both failed again, with their reasons', async ({ page, api }) => {
    const move = retryOf(api, RUN_2418_ISSUES.slice(0, 2));
    const runs = new RunsPage(page);
    await retry(page, runs);

    move('ended');
    await untilNotified(page, runs);

    await expect(runs.toastTitle).toHaveText('Both sources failed again');
    await expect(runs.toastText).toHaveText(
      'Redeemed Run Club Toronto returned 404 and Covenant Cycling Collective timed out.',
    );
    await expect(runs.toastAction).toHaveText('Edit sources');
    await expect(runs.toast).toHaveClass(/toast--danger/);
  });
});
