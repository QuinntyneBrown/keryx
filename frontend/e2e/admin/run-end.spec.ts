// Acceptance Test
// Traces to: L2-058 AC3; L2-043 AC6
// Description: When the run in progress ends, the screen learns of it without a reload: Runs shows
// the final outcome and reads its list again, Overview's card returns to Last run, and "Run now"
// is back.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import { RUN_HISTORY, RUNNING_NOW, RUNNING_RUN, runDetail, runningDetail } from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

const ENDED_RUN = {
  ...RUNNING_RUN,
  outcome: 'succeeded',
  finished_at: '2026-11-28T14:50:12Z',
  duration_ms: 252_000,
  added: 47,
  sources_checked: 64,
};

test.describe('A run ends', () => {
  test('shows the final outcome on Runs and lists it', async ({ page, api }) => {
    await page.clock.install({ time: RUNNING_NOW });
    let ended = false;
    api.on('GET', '/admin/v1/runs', () => ({
      json: {
        items: [ended ? ENDED_RUN : RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)],
        total: 15,
        page: 1,
        page_size: 14,
      },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => {
      const answer = ended ? runDetail('run_2419', { ...ENDED_RUN, stages: [] }) : runningDetail();
      return { json: answer };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailBadge).toHaveText('Running');
    await expect(runs.runNow).toHaveText('Collecting…');

    ended = true;
    await expect(async () => {
      await page.clock.runFor(1000);
      await expect(runs.detailBadge).not.toHaveText('Running', { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 10_000 });

    await expect(runs.runWords.first()).not.toHaveText('Running:');
    await expect(runs.runSpinners).toHaveCount(0);
    await expect(runs.runNow).toHaveText('Run now');
    await expect(runs.runNow).toBeEnabled();
  });

  test('learns of the end while another run is shown on Runs', async ({ page, api }) => {
    await page.clock.install({ time: RUNNING_NOW });
    let ended = false;
    api.on('GET', '/admin/v1/runs', () => ({
      json: {
        items: [ended ? ENDED_RUN : RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)],
        total: 15,
        page: 1,
        page_size: 14,
      },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({
      json: ended ? runDetail('run_2419', { ...ENDED_RUN, stages: [] }) : runningDetail(),
    }));
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await expect(runs.runNow).toHaveText('Collecting…');

    ended = true;
    await expect(async () => {
      await page.clock.runFor(1000);
      await expect(runs.runNow).toHaveText('Run now', { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 10_000 });

    await expect(runs.runSpinners).toHaveCount(0);
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
  });

  test('returns Overview’s card to Last run', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    let ended = false;
    const running = overviewRun({
      id: 'run_2419',
      started_at: '2026-11-28T14:46:00Z',
      finished_at: null,
      duration_ms: null,
      outcome: 'running',
      added: 21,
      sources_ok: 38,
      sources_checked: 38,
      sources_total: 64,
    });
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus(
        ended
          ? {
              last_run: overviewRun({
                id: 'run_2419',
                started_at: '2026-11-28T14:46:00Z',
                finished_at: '2026-11-28T14:50:12Z',
                outcome: 'succeeded',
                sources_ok: 64,
              }),
            }
          : { current_run: running },
      ),
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({
      json: ended ? runDetail('run_2419', { ...ENDED_RUN, stages: [] }) : runningDetail(),
    }));
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.eyebrow).toHaveText('Current run');
    await expect(overview.runNow).toHaveText('Collecting…');

    ended = true;
    await expect(async () => {
      await page.clock.runFor(1000);
      await expect(overview.eyebrow).toHaveText('Last run', { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 10_000 });

    await expect(overview.headline).toHaveText('Completed — all sources collected');
    await expect(overview.runNow).toHaveText('Run now');
  });
});
