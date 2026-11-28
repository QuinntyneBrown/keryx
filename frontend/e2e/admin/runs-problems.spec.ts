// Acceptance Test
// Traces to: L2-056 AC2, AC3
// Description: "With problems" lists only runs that failed, completed with issues or had a failed
// source, keeps the choice in the URL, and offers the way back when none had a problem.

import { expect, test, type ApiHandlers } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_HISTORY } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

/** The runs endpoint as the server filters it: `problems=true` keeps runs with a problem. */
function answerRuns(api: ApiHandlers, asked: string[], history = RUN_HISTORY): void {
  api.on('GET', '/admin/v1/runs', (request) => {
    const params = new URL(request.url()).searchParams;
    asked.push(params.toString());
    const items =
      params.get('problems') === 'true'
        ? history.filter(
            (run) => run['outcome'] !== 'succeeded' || (run['sources_failed'] as number) > 0,
          )
        : history;
    return { json: { items, total: items.length, page: 1, page_size: 14 } };
  });
}

test.describe('Runs with problems', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('lists only runs with problems', { tag: '@critical' }, async ({ page, api }) => {
    const asked: string[] = [];
    answerRuns(api, asked);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.allRuns).toHaveAttribute('aria-current', 'true');
    await expect(runs.runs).toHaveCount(14);

    await runs.withProblems.click();

    await expect.poll(() => runs.search()).toBe('?problems=true');
    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
    await expect(runs.allRuns).not.toHaveAttribute('aria-current');
    await expect(runs.runTitles).toHaveText([
      'Today, 6:00 AM',
      'Yesterday, 12:00 PM',
      'Yesterday, 6:00 AM',
      'Yesterday, 12:00 AM',
      'Thu, Nov 26, 6:00 PM',
      'Thu, Nov 26, 12:00 PM',
    ]);
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    // A run chosen among them keeps the filter.
    await runs.runs.nth(1).click();

    await expect.poll(() => runs.search()).toBe('?problems=true&run=run_2414');
    await expect(runs.runs).toHaveCount(6);
    await expect(runs.detailHeading).toHaveText('Yesterday, 12:00 PM');
    expect(asked).toEqual([
      'worker=collection&page=1&page_size=14',
      'worker=collection&problems=true&page=1&page_size=14',
    ]);
  });

  test('keeps the choice in the URL', async ({ page, api }) => {
    const asked: string[] = [];
    answerRuns(api, asked);
    const runs = new RunsPage(page);

    await runs.open('?problems=true&run=run_2414');

    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
    await expect(runs.runs).toHaveCount(6);
    await expect(runs.current.locator('.list__title')).toHaveText('Yesterday, 12:00 PM');
    await expect(runs.detailHeading).toHaveText('Yesterday, 12:00 PM');

    await runs.allRuns.click();

    await expect.poll(() => runs.search()).toBe('');
    await expect(runs.runs).toHaveCount(14);
    await expect(runs.current.locator('.list__title')).toHaveText('Today, 6:00 AM');
    expect(asked).toEqual([
      'worker=collection&problems=true&page=1&page_size=14',
      'worker=collection&page=1&page_size=14',
    ]);
  });

  test('offers the way back when no run had a problem', async ({ page, api }) => {
    const clean = RUN_HISTORY.filter((run) => run['outcome'] === 'succeeded').map((run) => ({
      ...run,
      sources_failed: 0,
    }));
    answerRuns(api, [], clean);
    const runs = new RunsPage(page);

    await runs.open('?problems=true');

    await expect(runs.runs).toHaveCount(0);
    await expect(runs.noResults.getByRole('heading')).toHaveText('No runs with problems');
    await expect(runs.noResults).toContainText('Every run Keryx has on record finished cleanly.');
    await expect(runs.chooseRun.getByRole('heading')).toHaveText('Choose a run');
    await expect(runs.detail).toHaveCount(0);

    await runs.noResults.getByRole('link', { name: 'Show all runs' }).click();

    await expect.poll(() => runs.search()).toBe('');
    await expect(runs.allRuns).toHaveAttribute('aria-current', 'true');
    await expect(runs.runs).toHaveCount(clean.length);
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
  });
  test('shows no run when switching to a filter none matches', async ({ page, api }) => {
    const clean = RUN_HISTORY.filter((run) => run['outcome'] === 'succeeded').map((run) => ({
      ...run,
      sources_failed: 0,
    }));
    answerRuns(api, [], clean);
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');

    await runs.withProblems.click();

    await expect(runs.noResults.getByRole('heading')).toHaveText('No runs with problems');
    await expect(runs.detail).toHaveCount(0);
    await expect(runs.chooseRun).toHaveCount(1);
  });

  // The list shown is the filter chosen last, never an earlier one or one answered late.
  test('shows the filter chosen last when an earlier answer is late', async ({ page, api }) => {
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    answerRuns(api, []);
    api.on('GET', '/admin/v1/runs', async (request) => {
      const problems = new URL(request.url()).searchParams.get('problems') === 'true';
      if (problems) await held;
      const items = problems ? RUN_HISTORY.slice(0, 1) : RUN_HISTORY;
      return { json: { items, total: items.length, page: 1, page_size: 14 } };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.runs).toHaveCount(14);

    await runs.withProblems.click();
    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
    await expect(runs.runs).toHaveCount(0);
    await runs.allRuns.click();
    await expect(runs.runs).toHaveCount(14);
    const late = page.waitForResponse((response) => response.url().includes('problems=true'));
    release();
    await late;

    await expect(runs.allRuns).toHaveAttribute('aria-current', 'true');
    await expect(runs.runs).toHaveCount(14);
  });
});
