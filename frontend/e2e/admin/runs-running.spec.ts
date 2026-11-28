// Acceptance Test
// Traces to: L2-058 AC1
// Description: A run in progress is first in the list as "Running" with how far it has got, and
// its detail shows it running for so long, fetch running with the sources checked so far and the
// other stages waiting, publish naming the run the API still serves.

import { expect, test } from '../fixtures/test';
import { RUN_HISTORY, RUNNING_NOW, RUNNING_RUN, runningDetail } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Running run', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(RUNNING_NOW);
    api.on('GET', '/admin/v1/runs', () => ({
      json: {
        items: [RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)],
        total: 15,
        page: 1,
        page_size: 14,
      },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({ json: runningDetail() }));
  });

  test('lists the run in progress first, with how far it has got', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.runTitles.first()).toHaveText('Today, 9:46 AM');
    await expect(runs.runWords.first()).toHaveText('Running:');
    await expect(runs.runMetas.first()).toHaveText(
      '38 of 64 sources checked · 1m 32s so far · manual',
    );
    await expect(runs.runSpinners).toHaveCount(1);
    await expect(runs.rowSpinner(0)).toHaveCount(1);
    await expect(runs.rowFlag(0)).toHaveCount(0);
    // Small, as the icons beside it, in the colour of information.
    const look = await runs.spinnerLook();
    expect(look.width).toBe(look.iconWidth);
    expect(look.color).toBe(look.info);
    // A finished run reads as before.
    await expect(runs.runMetas.nth(1)).toHaveText('47 new · 4m 12s');
  });

  test('shows its detail running, with the stages still to come waiting', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.detailHeading).toHaveText('Today, 9:46 AM');
    await expect(runs.detailBadge).toHaveText('Running');
    await expect(runs.detailMeta).toHaveText('run_2419 · Manual · running for 1m 32s');
    await expect(runs.tileValues).toHaveText(['21', '4', '38', '0']);
    await expect(runs.stepStates()).resolves.toEqual([
      'running',
      'skipped',
      'skipped',
      'skipped',
      'skipped',
      'skipped',
    ]);
    await expect(runs.stepWords).toHaveText([
      'Running:',
      'Not run:',
      'Not run:',
      'Not run:',
      'Not run:',
      'Not run:',
    ]);
    await expect(runs.stepTimes).toHaveText(['1:32', '—', '—', '—', '—', '—']);
    await expect(runs.stepDetails).toHaveText([
      '38 of 64 sources checked',
      'Waiting',
      'Waiting',
      'Waiting',
      'Waiting',
      'Waiting — API is serving the 6:00 AM run',
    ]);
  });

  // The API serves the newest run that finished without failing: a failed run published nothing.
  test('names the run the API still serves, passing over a failed one', async ({ page, api }) => {
    const [failed, served] = ['run_2414', 'run_2412'].map((id) =>
      RUN_HISTORY.find((run) => run.id === id),
    );
    api.on('GET', '/admin/v1/runs', () => ({
      json: { items: [RUNNING_RUN, failed, served], total: 3, page: 1, page_size: 14 },
    }));
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.stepDetails.last()).toHaveText('Waiting — API is serving the 12:00 AM run');
  });
});
