// Acceptance Test
// Traces to: L2-076 AC5; L2-058 AC4
// Description: When the time of the next collection passes, the shell learns of the scheduled run
// on any screen, follows it, and says once how it ended.

import type { Page } from '@playwright/test';
import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW, overviewRun, overviewStatus } from '../fixtures/overview';
import { COUNTS } from '../fixtures/articles';
import { runDetail, runningDetail, shellSchedule } from '../fixtures/runs';
import { ArticlesPage } from '../page-objects/admin/articles.page';

/**
 * Waits until every request the page sent so far has reached the server: a request it sends now
 * arrives after them.
 */
async function settled(page: Page): Promise<void> {
  await page.evaluate(() => fetch('/admin/v1/regions').then((response) => response.ok));
}

test.describe('A scheduled run', () => {
  test('is followed on any screen and its end said once', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    let phase: 'waiting' | 'running' | 'ended' = 'waiting';
    let statusReads = 0;
    // The 12:00 PM run starts on time; the schedule then names 6:00 PM.
    api.on('GET', '/admin/v1/overview/schedule', () => ({
      json: shellSchedule(phase === 'waiting' ? {} : { next_run_at: '2026-11-28T23:00:00Z' }),
    }));
    api.on('GET', '/admin/v1/overview/status', () => {
      statusReads += 1;
      return {
        json: overviewStatus(
          phase === 'running'
            ? {
                current_run: overviewRun({
                  id: 'run_2419',
                  started_at: '2026-11-28T17:00:00Z',
                  finished_at: null,
                  duration_ms: null,
                  outcome: 'running',
                }),
              }
            : {},
        ),
      };
    });
    api.on('GET', '/admin/v1/runs/run_2419', () => ({
      json:
        phase === 'running'
          ? runningDetail()
          : runDetail('run_2419', { outcome: 'succeeded', added: 47, sources_failed: 0 }),
    }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    const articles = new ArticlesPage(page);
    await articles.open();
    // Before its time, nothing asks whether a run is in progress.
    await page.clock.runFor(60_000);
    await settled(page);
    expect(statusReads).toBe(0);

    phase = 'running';
    await page.clock.fastForward('02:14:30');
    await expect(async () => {
      await page.clock.runFor(1000);
      expect(statusReads).toBeGreaterThanOrEqual(1);
    }).toPass({ intervals: [0], timeout: 10_000 });

    phase = 'ended';
    await expect(async () => {
      await page.clock.runFor(1000);
      await expect(articles.toasts).toHaveCount(1, { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 10_000 });

    await expect(articles.toast.locator('.toast__title')).toHaveText('Collection finished');
    await page.clock.runFor(6000);
    await expect(articles.toasts).toHaveCount(1);
    // The next run is hours away: the shell asks no more.
    const reads = statusReads;
    await page.clock.runFor(120_000);
    await settled(page);
    expect(statusReads).toBe(reads);
  });
});
