// Acceptance Test
// Traces to: L2-061 AC1; L2-073 AC1
// Description: While Runs' first answers are slow, skeletons of the list and the detail stand in
// and the region is busy; a run chosen while its detail is slow shows the detail's skeleton, not
// the run shown before.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_STAGES, runDetail, RUN_HISTORY } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Runs loading', () => {
  test('shows skeletons while the first answers are slow', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    const filtered: string[] = [];
    api.on('GET', '/admin/v1/runs', async (request) => {
      if (new URL(request.url()).searchParams.has('problems')) filtered.push(request.url());
      await held;
      return { json: { items: RUN_HISTORY, total: 14, page: 1, page_size: 14 } };
    });
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', { stages: RUN_2418_STAGES }),
    }));
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.filter).toBeVisible();

    // Busy at once; the skeletons wait 300 ms (skeletonDelay, proven by its unit test).
    await expect(runs.body).toHaveAttribute('aria-busy', 'true');

    await expect(async () => {
      await page.clock.runFor(100);
      await expect(runs.runSkeletons).toHaveCount(10, { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 5000 });
    await expect(runs.loadingStatus).toHaveText('Loading runs…');
    await expect(runs.filter).toHaveAttribute('aria-disabled', 'true');
    // Nor by key: the disabled filter's links do nothing.
    await runs.withProblems.focus();
    await page.keyboard.press('Enter');
    await expect(runs.cardSkeleton).toHaveCount(1);
    await expect(runs.stepSkeletons).toHaveCount(6);
    const card = await runs.cardSkeleton.boundingBox();
    const row = await runs.runSkeletons.nth(1).boundingBox();
    const steps = await runs.stepSkeletons.first().boundingBox();

    await page.clock.resume();
    release();

    await expect(runs.runs).toHaveCount(14);
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await expect(runs.runSkeletons).toHaveCount(0);
    await expect(runs.cardSkeleton).toHaveCount(0);
    await expect(runs.stepSkeletons).toHaveCount(0);
    await expect(runs.body).not.toHaveAttribute('aria-busy');
    await expect(runs.loadingStatus).toHaveCount(0);
    await expect(runs.filter).not.toHaveAttribute('aria-disabled');
    expect(runs.search()).toBe('');
    expect(filtered).toEqual([]);
    // Nothing shifts: the card, the rows and the stages start where their skeletons did.
    expect((await runs.detail.locator('.card').boundingBox())?.y).toBe(card?.y);
    expect((await runs.runs.nth(1).boundingBox())?.y).toBe(row?.y);
    expect((await runs.steps.first().boundingBox())?.y).toBe(steps?.y);
  });

  test('shows the chosen run loading, not the run before it', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/runs/run_2417', async () => {
      await held;
      return { json: runDetail('run_2417') };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    await runs.runs.nth(1).click();

    await expect(runs.current.locator('.list__title')).toHaveText('Today, 12:00 AM');
    await expect(runs.detailHeading).toHaveCount(0);
    await expect(async () => {
      await page.clock.runFor(100);
      await expect(runs.cardSkeleton).toHaveCount(1, { timeout: 50 });
    }).toPass({ intervals: [0], timeout: 5000 });
    await expect(runs.stepSkeletons).toHaveCount(6);
    // The list is there: the page is not loading, only the run.
    await expect(runs.runSkeletons).toHaveCount(0);
    await expect(runs.loadingStatus).toHaveCount(0);

    await page.clock.resume();
    release();

    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
    await expect(runs.cardSkeleton).toHaveCount(0);
  });
  // A run's error belongs to it: choosing another clears it while that one loads.
  test('clears the error of a run while the next run loads', async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      status: 503,
      json: { status: 503, trace_id: 'req_1' },
    }));
    api.on('GET', '/admin/v1/runs/run_2417', async () => {
      await held;
      return { json: runDetail('run_2417') };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailError).toHaveCount(1);

    await runs.runs.nth(1).click();

    await expect(runs.current.locator('.list__title')).toHaveText('Today, 12:00 AM');
    await expect(runs.detailError).toHaveCount(0);
    release();
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
  });
  // Each wait starts afresh: once a slow read has ended, the next waits its own 300 ms, so a run
  // chosen next never flashes a skeleton at once.
  test('waits again before each skeleton', async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    const holds = new Map<string, () => void>();
    const hold = (key: string) => new Promise<void>((resolve) => holds.set(key, resolve));
    const list = hold('list');
    api.on('GET', '/admin/v1/runs', async () => {
      await list;
      return { json: { items: RUN_HISTORY, total: 14, page: 1, page_size: 14 } };
    });
    for (const id of ['run_2416', 'run_2415']) {
      const answer = hold(id);
      api.on('GET', `/admin/v1/runs/${id}`, async () => {
        await answer;
        return { json: runDetail(id) };
      });
    }
    const runs = new RunsPage(page);

    // The list is slow; the run the URL names answers at once.
    await runs.open('?run=run_2417');
    await expect(runs.runSkeletons).toHaveCount(10);
    holds.get('list')?.();
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');

    await runs.runs.nth(2).click();
    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 6:00 PM');
    expect(await runs.cardSkeleton.count()).toBe(0);
    await expect(runs.cardSkeleton).toHaveCount(1);
    holds.get('run_2416')?.();
    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');

    await runs.runs.nth(3).click();
    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 2:37 PM');
    expect(await runs.cardSkeleton.count()).toBe(0);
    await expect(runs.cardSkeleton).toHaveCount(1);
    holds.get('run_2415')?.();
    await expect(runs.detailHeading).toHaveText('Yesterday, 2:37 PM');
  });
});
