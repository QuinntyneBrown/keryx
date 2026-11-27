// Acceptance Test
// Traces to: L2-043 AC1, AC2, AC4, AC5
// Description: Overview opens on the status card: how the last run went, in words and an icon,
// with its new articles, sources OK and the next run.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

test.describe('Overview status card', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test(
    'reads a run that completed with 2 failing sources',
    { tag: '@critical' },
    async ({ page, api }) => {
      api.on('GET', '/admin/v1/overview/status', () => ({ json: overviewStatus() }));
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.heading).toHaveText('Overview');
      await expect(overview.navLink).toHaveAttribute('aria-current', 'page');
      await expect(overview.eyebrow).toHaveText('Last run');
      await expect(overview.headline).toHaveText('Completed with 2 failing sources');
      await expect(overview.meta).toHaveText('Today, 6:00 AM · took 4m 12s · run_2418');
      await expect(overview.statusIcon).toHaveClass(/status-card__icon--warning/);
      await expect(overview.statLabels).toHaveText(['New articles', 'Sources OK', 'Next run']);
      await expect(overview.statValues).toHaveText(['47', '62/64', '12:00 PM']);
      await expect(overview.statSubs).toHaveText(['in 2h 14m']);
    },
  );

  test('reads a run that collected every source', async ({ page, api }) => {
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({ last_run: overviewRun({ outcome: 'succeeded', sources_ok: 64 }) }),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.headline).toHaveText('Completed — all sources collected');
    await expect(overview.statusIcon).toHaveClass(/status-card__icon--success/);
    await expect(overview.statValues).toHaveText(['47', '64/64', '12:00 PM']);
  });

  test('reads a run that failed with its time, duration and id', async ({ page, api }) => {
    api.on('GET', '/admin/v1/overview/status', () => ({
      json: overviewStatus({
        last_run: overviewRun({
          outcome: 'failed',
          failure_code: 'store_failed',
          duration_ms: 48_000,
          added: 0,
          sources_ok: 0,
        }),
      }),
    }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.headline).toHaveText('Run failed — database unreachable');
    await expect(overview.meta).toHaveText('Today, 6:00 AM · took 0m 48s · run_2418');
    await expect(overview.statusIcon).toHaveClass(/status-card__icon--danger/);
    await expect(overview.statValues).toHaveText(['0', '0/64', '12:00 PM']);
  });

  test('counts down to the next run as time passes', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    api.on('GET', '/admin/v1/overview/status', () => ({ json: overviewStatus() }));
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.statSubs).toHaveText(['in 2h 14m']);

    await page.clock.runFor(60_000);

    await expect(overview.statSubs).toHaveText(['in 2h 13m']);
  });
});
