// Acceptance Test
// Traces to: L2-047 AC1; L2-006 AC1 (through the client)
// Description: "Run now" on Runs or Overview starts a collection: the button is busy while the
// request is out, one POST goes, "Collection started" says how many sources and about how long,
// and the screen shows the new run in progress without a reload.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import {
  RUN_HISTORY,
  RUN_STARTED,
  RUNNING_NOW,
  RUNNING_RUN,
  runningDetail,
} from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Run now', () => {
  test('starts a run from Runs and shows it in progress', async ({ page, api }) => {
    await page.clock.setFixedTime(RUNNING_NOW);
    let started = false;
    let posts = 0;
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('POST', '/admin/v1/runs', async (request) => {
      posts += 1;
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      await held;
      started = true;
      return { status: 202, json: RUN_STARTED };
    });
    api.on('GET', '/admin/v1/runs', () => ({
      json: {
        items: started ? [RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)] : RUN_HISTORY,
        total: 15,
        page: 1,
        page_size: 14,
      },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({ json: runningDetail() }));
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    await expect(runs.runNow).toHaveText('Run now');
    await runs.runNow.click();

    await expect(runs.runNow).toHaveAttribute('aria-busy', 'true');
    release();

    await expect(runs.toastTitle).toHaveText('Collection started');
    await expect(runs.toastText).toHaveText(
      'Checking 64 sources. This usually takes about 4 minutes.',
    );
    await expect.poll(() => runs.search()).toBe('?run=run_2419');
    await expect(runs.runTitles.first()).toHaveText('Today, 9:46 AM');
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 9:46 AM');
    await expect(runs.detailBadge).toHaveText('Running');
    expect(posts).toBe(1);
  });

  test('starts a run from Overview and shows it in progress', async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    let started = false;
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('POST', '/admin/v1/runs', () => {
      started = true;
      return { status: 202, json: { ...RUN_STARTED, estimated_duration_ms: null } };
    });
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus(
        started
          ? {
              current_run: overviewRun({
                id: 'run_2419',
                started_at: '2026-11-28T14:46:00Z',
                finished_at: null,
                duration_ms: null,
                outcome: 'running',
                added: 0,
                sources_ok: 0,
                sources_checked: 0,
                sources_total: 64,
              }),
            }
          : {},
      ),
    }));
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.eyebrow).toHaveText('Last run');

    await overview.runNow.click();

    await expect(overview.toastTitle).toHaveText('Collection started');
    await expect(overview.toastText).toHaveText('Checking 64 sources.');
    await expect(overview.eyebrow).toHaveText('Current run');
    await expect(overview.headline).toHaveText('Collecting news now…');
  });
});
