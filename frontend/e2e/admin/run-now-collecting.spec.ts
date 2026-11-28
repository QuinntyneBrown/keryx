// Acceptance Test
// Traces to: L2-058 AC4; L2-047 AC4 (no start while a run is in progress)
// Description: While a run is in progress, "Run now" is a disabled, busy "Collecting…" on Runs and
// Overview, and Runs' header says when the run started.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import { RUN_HISTORY, RUNNING_NOW, RUNNING_RUN, runningDetail } from '../fixtures/runs';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Collecting', () => {
  test('reads Collecting… on Runs while a run is in progress', async ({ page, api }) => {
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
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.runNow).toHaveText('Collecting…');
    await expect(runs.runNow).toBeDisabled();
    await expect(runs.runNow).toHaveAttribute('aria-busy', 'true');
    await expect(runs.description).toHaveText('A collection is running now · started 9:46 AM');
  });

  // A filtered list's newest run says nothing about whether one is in progress.
  test('still reads Collecting… with the problem runs shown', async ({ page, api }) => {
    await page.clock.setFixedTime(RUNNING_NOW);
    api.on('GET', '/admin/v1/runs', (request) => {
      const problems = new URL(request.url()).searchParams.get('problems') === 'true';
      const items = problems
        ? RUN_HISTORY.filter((run) => run['outcome'] !== 'succeeded')
        : [RUNNING_RUN, ...RUN_HISTORY.slice(0, 13)];
      return { json: { items, total: items.length, page: 1, page_size: 14 } };
    });
    api.on('GET', '/admin/v1/runs/run_2419', () => ({ json: runningDetail() }));
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.runNow).toHaveText('Collecting…');

    await runs.withProblems.click();

    await expect.poll(() => runs.search()).toContain('problems=true');
    await expect(runs.runTitles.first()).not.toHaveText('Today, 9:46 AM');
    await expect(runs.runNow).toHaveText('Collecting…');
  });

  test('reads Run now on Runs when no run is in progress', async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.runs).toHaveCount(14);
    await expect(runs.runNow).toHaveText('Run now');
    await expect(runs.runNow).toBeEnabled();
    await expect(runs.description).toHaveText(
      'Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)',
    );
  });

  test('reads Collecting… on Overview while a run is in progress', async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({
        current_run: overviewRun({
          id: 'run_2419',
          started_at: '2026-11-28T14:46:00Z',
          finished_at: null,
          duration_ms: null,
          outcome: 'running',
          added: 21,
          sources_ok: 38,
          sources_checked: 38,
          sources_total: 64,
        }),
      }),
    }));
    const overview = new OverviewPage(page);
    await overview.open();

    await expect(overview.eyebrow).toHaveText('Current run');
    await expect(overview.runNow).toHaveText('Collecting…');
    await expect(overview.runNow).toBeDisabled();
  });
});
