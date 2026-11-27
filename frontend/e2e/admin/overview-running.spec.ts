// Acceptance Test
// Traces to: L2-043 AC3, AC5
// Description: When Overview opens on a run in progress, the card reads Current run with how far
// it has got, in words and a progress bar with an accessible value.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

test.describe('Overview current run', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('reads the run in progress and how far it has got', async ({ page, api }) => {
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
    await expect(overview.headline).toHaveText('Collecting news now…');
    await expect(overview.meta).toHaveText('Started 9:46 AM · 38 of 64 sources checked');
    await expect(overview.statusIcon).toHaveClass(/status-card__icon--info/);
    await expect(overview.spinner).toHaveAttribute('aria-hidden', 'true');
    await expect(overview.statValues).toHaveText(['21', '38/64', '12:00 PM']);
    await expect(overview.progress).toHaveAccessibleName('Sources checked');
    await expect(overview.progress).toHaveAttribute('aria-valuemin', '0');
    await expect(overview.progress).toHaveAttribute('aria-valuemax', '64');
    await expect(overview.progress).toHaveAttribute('aria-valuenow', '38');
    expect(await overview.progressShare()).toBe(59);
  });

  // Checked counts every source tried, OK only those that answered; the bar rounds to the nearest
  // whole percent (40 of 64 is 62.5%).
  test('tells sources checked from sources OK', async ({ page, api }) => {
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({
        current_run: overviewRun({
          id: 'run_2419',
          started_at: '2026-11-28T14:46:00Z',
          finished_at: null,
          duration_ms: null,
          outcome: 'running',
          added: 21,
          sources_ok: 37,
          sources_checked: 40,
          sources_total: 64,
        }),
      }),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.meta).toHaveText('Started 9:46 AM · 40 of 64 sources checked');
    await expect(overview.statValues).toHaveText(['21', '37/64', '12:00 PM']);
    await expect(overview.progress).toHaveAttribute('aria-valuenow', '40');
    expect(await overview.progressShare()).toBe(63);
  });

  test('draws no progress bar for a run that has ended', async ({ page }) => {
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.eyebrow).toHaveText('Last run');
    await expect(overview.progress).toHaveCount(0);
    await expect(overview.spinner).toHaveCount(0);
  });
});
