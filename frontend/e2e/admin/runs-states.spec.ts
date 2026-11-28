// Acceptance Test
// Traces to: L2-061 AC2, AC3, AC4; L2-073 AC1, AC3
// Description: Runs with no run says so and names the first scheduled time; a failed list shows
// the page error with Try again and Go to overview; a failed detail leaves the list usable and
// shows its own error with Try again.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { runDetail, RUN_HISTORY } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

const FAILURE = { status: 503, json: { status: 503, trace_id: 'req_c03d5512' } };

test.describe('Runs states', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('says there is no run yet and when the first starts', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => ({
      json: { items: [], total: 0, page: 1, page_size: 14 },
    }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.pageStateTitle).toHaveText('No runs yet');
    await expect(runs.pageStateText).toHaveText(
      'A run is one pass over every source. The first one starts at 12:00 PM. Each run is recorded here with what it added and what went wrong.',
    );
    await expect(runs.filter).toHaveCount(0);
    await expect(runs.pageStateActions).toHaveText(['Add a source']);
    await expect(runs.pageStateActions).toHaveAttribute('href', '/sources');
  });

  test('shows the page error when the list fails', { tag: '@critical' }, async ({ page, api }) => {
    let fail = true;
    let answer: () => void = () => undefined;
    api.on('GET', '/admin/v1/runs', async () => {
      if (fail) return FAILURE;
      await new Promise<void>((resolve) => (answer = resolve));
      return { json: { items: RUN_HISTORY, total: 14, page: 1, page_size: 14 } };
    });
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.pageStateTitle).toHaveText("Runs didn't load");
    await expect(runs.pageState).toHaveClass(/empty--error/);
    await expect(runs.pageStateText).toHaveText(
      "Keryx couldn't reach its API just now. Collection keeps running on schedule; only this screen is affected. Try again, and if it keeps failing, send the reference below to whoever runs Keryx.",
    );
    await expect(runs.pageStateReference).toHaveText('Reference req_c03d5512');
    await expect(runs.pageStateActions).toHaveText(['Try again', 'Go to overview']);
    await expect(runs.filter).toHaveCount(0);

    fail = false;
    await runs.pageStateActions.first().click();

    await expect(runs.pageStateActions.first()).toHaveAttribute('aria-busy', 'true');
    answer();

    await expect(runs.pageState).toHaveCount(0);
    await expect(runs.runs).toHaveCount(14);
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
  });

  test('goes to Overview from the page error', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => FAILURE);
    const runs = new RunsPage(page);
    await runs.open();

    await runs.pageStateActions.nth(1).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(runs.heading).toHaveText('Overview');
  });

  test('keeps the list usable when a run fails to load', async ({ page, api }) => {
    let fail = true;
    api.on('GET', '/admin/v1/runs/run_2418', () =>
      fail ? FAILURE : { json: runDetail('run_2418') },
    );
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.detailError.locator('.alert__title')).toHaveText("This run didn't load");
    await expect(runs.detailError).toHaveAttribute('role', 'alert');
    await expect(runs.detailError).toContainText(
      "The run history is up to date; only this run's detail failed. Reference req_c03d5512",
    );
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');

    // The list still works: another run loads.
    await runs.runs.nth(1).click();
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
    await expect(runs.detailError).toHaveCount(0);

    // Back on the failed run, Try again reads it again.
    await runs.runs.nth(0).click();
    await expect(runs.detailError).toHaveCount(1);
    fail = false;
    await runs.detailError.getByRole('button', { name: 'Try again' }).click();

    await expect(runs.detailError).toHaveCount(0);
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');
  });
  test('names no time when the schedule is not read', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => ({
      json: { items: [], total: 0, page: 1, page_size: 14 },
    }));
    api.on('GET', '/admin/v1/overview/schedule', () => FAILURE);
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.pageStateText).toHaveText(
      'A run is one pass over every source. The first one starts at the next scheduled time. Each run is recorded here with what it added and what went wrong.',
    );
  });

  // A failure answered after a newer read stays out of sight: the list and the run shown are the
  // ones chosen last.
  test('ignores failures that arrive after a newer choice', async ({ page, api }) => {
    let releaseList: () => void = () => undefined;
    const listHeld = new Promise<void>((resolve) => (releaseList = resolve));
    let releaseRun: () => void = () => undefined;
    const runHeld = new Promise<void>((resolve) => (releaseRun = resolve));
    api.on('GET', '/admin/v1/runs', async (request) => {
      if (new URL(request.url()).searchParams.get('problems') === 'true') {
        await listHeld;
        return FAILURE;
      }
      return { json: { items: RUN_HISTORY, total: 14, page: 1, page_size: 14 } };
    });
    api.on('GET', '/admin/v1/runs/run_2417', async () => {
      await runHeld;
      return FAILURE;
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    await runs.runs.nth(1).click();
    await runs.runs.nth(2).click();
    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');
    const lateRun = page.waitForResponse('**/admin/v1/runs/run_2417');
    releaseRun();
    await lateRun;
    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');
    await expect(runs.detailError).toHaveCount(0);

    await runs.withProblems.click();
    await runs.allRuns.click();
    await expect(runs.runs).toHaveCount(14);
    const lateList = page.waitForResponse((response) => response.url().includes('problems=true'));
    releaseList();
    await lateList;

    await expect(runs.pageState).toHaveCount(0);
    await expect(runs.runs).toHaveCount(14);
  });

  // A run's failure belongs to that run: switching the list clears it.
  test('clears the error of a run when the list is switched', async ({ page, api }) => {
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/runs/run_2418', () => FAILURE);
    api.on('GET', '/admin/v1/runs', async (request) => {
      if (new URL(request.url()).searchParams.get('problems') === 'true') await held;
      return { json: { items: RUN_HISTORY, total: 14, page: 1, page_size: 14 } };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailError).toHaveCount(1);

    await runs.withProblems.click();

    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
    await expect(runs.detailError).toHaveCount(0);
    release();
  });
});
