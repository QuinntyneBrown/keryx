// Acceptance Test
// Traces to: L2-056 AC7; L2-057 AC7
// Description: Below 1024 px a chosen run takes the list's place with a way back to the list,
// focus follows the person both ways, nothing scrolls sideways, and the tiles sit two by two
// below 640 px and four in a row from 640 px.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Runs on narrow screens', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('shows the list alone, then the chosen run with a way back', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.runs).toHaveCount(14);
    await expect(runs.detail).toBeHidden();
    await expect(runs.back).toBeHidden();
    expect(await runs.scrollsSideways()).toBe(false);

    await runs.runs.nth(1).focus();
    await page.keyboard.press('Enter');

    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
    await expect(runs.history).toBeHidden();
    await expect(runs.back).toBeVisible();
    await expect(runs.back).toHaveAttribute('href', '/runs');
    await expect(runs.back).toBeFocused();
    expect(await runs.scrollsSideways()).toBe(false);
    // Two rows of two below 640 px.
    const boxes = await Promise.all([0, 1, 2, 3].map(async (i) => runs.tiles.nth(i).boundingBox()));
    expect(boxes[0]?.y).toBe(boxes[1]?.y);
    expect(boxes[2]?.y).toBe(boxes[3]?.y);
    expect(boxes[2]?.y).toBeGreaterThan(boxes[0]?.y ?? 0);

    await page.keyboard.press('Enter');

    await expect.poll(() => runs.search()).toBe('');
    await expect(runs.history).toBeVisible();
    await expect(runs.detail).toBeHidden();
    // Focus returns to the run it came from.
    await expect(runs.runs.nth(1)).toBeFocused();
  });

  test('keeps the filter on the way back', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const runs = new RunsPage(page);
    await runs.open('?problems=true&run=run_2416');

    await expect(runs.history).toBeHidden();
    await expect(runs.back).toHaveAttribute('href', '/runs?problems=true');
    await runs.back.click();

    await expect.poll(() => runs.search()).toBe('?problems=true');
    await expect(runs.history).toBeVisible();
    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
  });

  test('sets four tiles in a row from 640 px', async ({ page }) => {
    await page.setViewportSize({ width: 800, height: 900 });
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await expect(runs.history).toBeHidden();
    const boxes = await Promise.all([0, 3].map(async (i) => runs.tiles.nth(i).boundingBox()));
    expect(boxes[0]?.y).toBe(boxes[1]?.y);
    expect(await runs.scrollsSideways()).toBe(false);
  });

  test('shows both side by side from 1024 px, with no way back', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
    await expect(runs.history).toBeVisible();
    await expect(runs.back).toBeHidden();
  });
});
