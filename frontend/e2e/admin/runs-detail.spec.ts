// Acceptance Test
// Traces to: L2-056 AC4, AC5; L2-057 AC1
// Description: Runs shows the newest run's detail, or the run the URL names; choosing a run marks
// it current, names it in the URL and shows its outcome, reference and four tiles.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { runDetail } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Run detail', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('shows the newest run when none is named', { tag: '@critical' }, async ({ page, api }) => {
    const asked: string[] = [];
    api.on('GET', /^\/admin\/v1\/runs\/[^/]+$/, (request) => {
      const id = new URL(request.url()).pathname.split('/').pop() ?? '';
      asked.push(id);
      return { json: runDetail(id) };
    });
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.current).toHaveCount(1);
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await expect(runs.detailBadge).toHaveText('Completed with issues');
    await expect(runs.detailBadge).toHaveClass(/badge--warning/);
    await expect(runs.detailMeta).toHaveText('run_2418 · Schedule · took 4m 12s');
    await expect(runs.tileLabels).toHaveText([
      'New articles',
      'Duplicates skipped',
      'Sources OK',
      'Sources failed',
    ]);
    await expect(runs.tileValues).toHaveText(['47', '18', '62', '2']);
    await expect(runs.tileValues.nth(3)).toHaveClass(/stat__value--danger/);
    // The tiles are the base size, as the mock's `stat__value` with no size modifier.
    await expect(runs.tileValues.first()).toHaveClass('stat__value');

    // Choosing the run already shown reads it no second time.
    await runs.runs.nth(0).click();
    await expect.poll(() => runs.search()).toBe('?run=run_2418');
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');
    expect(asked).toEqual(['run_2418']);
  });

  test('shows the run the URL names', async ({ page }) => {
    const runs = new RunsPage(page);

    await runs.open('?run=run_2414');

    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 12:00 PM');
    await expect(runs.detailHeading).toHaveText('Yesterday, 12:00 PM');
    await expect(runs.detailBadge).toHaveText('Failed');
    await expect(runs.detailBadge).toHaveClass(/badge--danger/);
    await expect(runs.detailMeta).toHaveText('run_2414 · Schedule · took 0m 48s');
  });

  test('shows a run when it is chosen', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    await runs.runs.nth(3).click();

    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 2:37 PM');
    await expect(runs.runs.nth(0)).not.toHaveAttribute('aria-current');
    await expect.poll(() => runs.search()).toBe('?run=run_2415');
    await expect(runs.detailHeading).toHaveText('Yesterday, 2:37 PM');
    await expect(runs.detailBadge).toHaveText('Succeeded');
    await expect(runs.detailMeta).toHaveText('run_2415 · Manual · took 3m 40s');
    await expect(runs.tileValues).toHaveText(['8', '5', '64', '0']);
    await expect(runs.tileValues.nth(3)).not.toHaveClass(/stat__value--danger/);
  });
  test('shows the newest run again when the URL names none', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open('?run=run_2414');
    await expect(runs.detailHeading).toHaveText('Yesterday, 12:00 PM');

    await runs.navLink.click();

    await expect.poll(() => runs.search()).toBe('');
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
  });

  // A run chosen after another shows, whichever answer arrives last.
  test('shows the run chosen last when an earlier answer is late', async ({ page, api }) => {
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
    await runs.runs.nth(2).click();
    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');
    const late = page.waitForResponse('**/admin/v1/runs/run_2417');
    release();
    await late;

    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');
    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 6:00 PM');
  });
});
