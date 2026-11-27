// Acceptance Test
// Traces to: L2-044 AC1, AC2, AC3, AC6
// Description: Overview's run chart shows the new articles of the last 14 collection runs, oldest
// first, with each run's outcome in words as well as colour, inside the card at every width.

import { expect, test } from '../fixtures/test';
import { LAST_14_RUNS, OVERVIEW_NOW, runPage, runSummary } from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

test.describe('Overview run chart', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test(
    'shows the last 14 runs oldest first, each labelled by its time and count',
    { tag: '@critical' },
    async ({ page, api }) => {
      const asked: string[] = [];
      api.on('GET', '/admin/v1/runs', (request) => {
        asked.push(new URL(request.url()).search);
        return { json: runPage(LAST_14_RUNS, 2418) };
      });
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.chartCaption).toHaveText('New articles per run · last 14 runs');
      await expect(overview.bars).toHaveCount(14);
      expect(await overview.barLabels()).toEqual([
        'Wed, Nov 25 6:00 AM: 19 new',
        'Wed, Nov 25 12:00 PM: 26 new',
        'Wed, Nov 25 6:00 PM: 35 new',
        'Thu, Nov 26 12:00 AM: 9 new',
        'Thu, Nov 26 6:00 AM: 41 new',
        'Thu, Nov 26 12:00 PM: 22 new',
        'Thu, Nov 26 6:00 PM: 28 new',
        'Yesterday 12:00 AM: 6 new',
        'Yesterday 6:00 AM: 39 new',
        'Yesterday 12:00 PM: failed',
        'Yesterday 2:37 PM: 8 new',
        'Yesterday 6:00 PM: 31 new',
        'Today 12:00 AM: 12 new',
        'Today 6:00 AM: 47 new',
      ]);
      expect(await overview.barStatuses()).toEqual([
        'success',
        'success',
        'success',
        'success',
        'success',
        'warning',
        'success',
        'success',
        'success',
        'failed',
        'success',
        'success',
        'success',
        'warning',
      ]);
      expect(asked).toEqual(['?worker=collection&page=1&page_size=14']);
    },
  );

  // L2-044 AC1, AC2: heights follow the counts against the best run (47 is the 56 px plot), and a
  // failed run is a 4 px stub whatever it stored before it stopped, marked by its shape and its
  // words as well as its colour.
  test('draws each bar to its count and a failed run as a stub', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => ({
      json: runPage([
        runSummary('run_3', '2026-11-28T11:00:00Z', 'succeeded', 47),
        runSummary('run_2', '2026-11-28T05:00:00Z', 'failed', 20),
        runSummary('run_1', '2026-11-27T23:00:00Z', 'succeeded', 6),
      ]),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.bars).toHaveCount(3);
    // 6 of 47 is 13% of 56 px, 7.28 px; failed is the 4 px stub; 47 is the whole plot.
    expect(await overview.barHeights()).toEqual([7, 4, 56]);
  });

  // L2-044 AC2, AC3: fewer than 14 runs show only those, and the run in progress reads "running".
  test('shows only the runs there are, with the one in progress', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => ({
      json: runPage([
        runSummary('run_2', '2026-11-28T14:40:00Z', 'running', 3),
        runSummary('run_1', '2026-11-28T11:00:00Z', 'succeeded', 12),
      ]),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.bars).toHaveCount(2);
    expect(await overview.barLabels()).toEqual(['Today 6:00 AM: 12 new', 'Today 9:40 AM: running']);
    expect(await overview.barStatuses()).toEqual(['success', 'running']);
  });

  // L2-044 AC6: at the narrowest viewport class all 14 bars fit inside the card.
  for (const width of [360, 576, 768, 992, 1200]) {
    test(`fits all 14 bars inside the card at ${width} px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 800 });
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.bars).toHaveCount(14);
      expect(await overview.chartFits()).toBe(true);
    });
  }
});
