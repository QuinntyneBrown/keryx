// Acceptance Test
// Traces to: L2-056 AC1, L2-042 AC1 (the schedule line)
// Description: Runs lists the 14 most recent collection runs, newest first, each with its outcome
// as an icon and a word, its start, what it added and how long it took, under the schedule.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_HISTORY, shellSchedule } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Runs list', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test(
    'lists the 14 most recent runs under the schedule',
    { tag: '@critical' },
    async ({ page, api }) => {
      const asked: string[] = [];
      api.on('GET', '/admin/v1/runs', (request) => {
        asked.push(new URL(request.url()).search);
        return { json: { items: RUN_HISTORY, total: 2418, page: 1, page_size: 14 } };
      });
      api.on('GET', '/admin/v1/overview/schedule', () => ({ json: shellSchedule() }));
      const runs = new RunsPage(page);

      await runs.open();

      await expect(runs.heading).toHaveText('Runs');
      await expect(page).toHaveTitle('Runs · Keryx');
      await expect(runs.navLink).toHaveAttribute('aria-current', 'page');
      await expect(runs.description).toHaveText(
        'Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)',
      );
      await expect(runs.runTitles).toHaveText([
        'Today, 6:00 AM',
        'Today, 12:00 AM',
        'Yesterday, 6:00 PM',
        'Yesterday, 2:37 PM',
        'Yesterday, 12:00 PM',
        'Yesterday, 6:00 AM',
        'Yesterday, 12:00 AM',
        'Thu, Nov 26, 6:00 PM',
        'Thu, Nov 26, 12:00 PM',
        'Thu, Nov 26, 6:00 AM',
        'Thu, Nov 26, 12:00 AM',
        'Wed, Nov 25, 6:00 PM',
        'Wed, Nov 25, 12:00 PM',
        'Wed, Nov 25, 6:00 AM',
      ]);
      await expect(runs.runMetas).toHaveText([
        '47 new · 4m 12s',
        '12 new · 3m 51s',
        '31 new · 4m 03s',
        '8 new · 3m 40s · manual',
        'Failed · 0m 48s',
        '39 new · 4m 22s',
        '6 new · 3m 58s',
        '28 new · 4m 10s',
        '22 new · 4m 31s',
        '41 new · 4m 05s',
        '9 new · 3m 49s',
        '35 new · 4m 14s',
        '26 new · 4m 19s',
        '19 new · 4m 02s',
      ]);
      await expect(runs.runWords.nth(0)).toHaveText('Completed with issues:');
      await expect(runs.runWords.nth(1)).toHaveText('Succeeded:');
      await expect(runs.runWords.nth(4)).toHaveText('Failed:');
      expect(await runs.runTones()).toEqual([
        'warning',
        'success',
        'success',
        'success',
        'danger',
        'success',
        'success',
        'success',
        'warning',
        'success',
        'success',
        'success',
        'success',
        'success',
      ]);
      await expect(runs.runs.nth(0).locator('.run-item__flag')).toHaveText('2 failed');
      await expect(runs.runs.nth(1).locator('.run-item__flag')).toHaveCount(0);
      await expect(runs.runFlags).toHaveText([
        '2 failed',
        '1 failed',
        '1 failed',
        '1 failed',
        '4 failed',
      ]);
      expect(asked).toEqual(['?worker=collection&page=1&page_size=14']);
    },
  );
  // The header leaves out what it could not read; the runs still read in Toronto time.
  test('lists the runs when the schedule fails', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs', () => ({
      json: { items: RUN_HISTORY, total: 2418, page: 1, page_size: 14 },
    }));
    api.on('GET', '/admin/v1/overview/schedule', () => ({ status: 500, json: {} }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.runTitles.first()).toHaveText('Today, 6:00 AM');
    await expect(runs.description).toHaveCount(0);
  });
  // The shell holds the schedule for the visit; going back to Runs does not ask again.
  test('asks for the schedule once across visits', async ({ page, api }) => {
    let asked = 0;
    api.on('GET', '/admin/v1/overview/schedule', () => {
      asked++;
      return { json: shellSchedule() };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.description).toHaveText(/^Scheduled every 6 hours/);

    await runs.overviewLink.click();
    await expect(runs.heading).toHaveText('Overview');
    await runs.navLink.click();

    await expect(runs.description).toHaveText(/^Scheduled every 6 hours/);
    expect(asked).toBe(1);
  });
});
