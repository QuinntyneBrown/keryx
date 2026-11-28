// Acceptance Test
// Traces to: L2-044 AC4, AC5; L2-045 AC3, AC6; L2-046 AC3 (Go to runs, mock error.html)
// Description: Overview leads to Runs: each bar opens its run, "All runs" opens the history, an
// issue card opens the run it belongs to, more than 5 open issues link to the runs with problems,
// and the page error offers "Go to runs".

import { expect, test } from '../fixtures/test';
import { issuePage, OPEN_ISSUES, openIssue, OVERVIEW_NOW } from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Overview links into Runs', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('opens a bar’s run by pointer, and another by keyboard', async ({ page }) => {
    const overview = new OverviewPage(page);
    const runs = new RunsPage(page);
    await overview.open();

    await expect(overview.barLinks).toHaveCount(14);
    // Oldest first: the last bar is today's 6:00 AM run, the first Wednesday's.
    await expect(overview.barLinks.last()).toHaveAttribute('href', '/runs?run=run_2418');
    await expect(overview.barLinks.first()).toHaveAttribute('href', '/runs?run=run_2405');
    await expect(overview.barLinks.last()).toHaveAccessibleName('Today 6:00 AM: 47 new');

    await overview.barLinks.last().click();
    await expect.poll(() => runs.search()).toBe('?run=run_2418');
    await expect(runs.detailHeading).toHaveText('Today, 6:00 AM');

    await page.goBack();
    await overview.barLinks.nth(12).focus();
    await page.keyboard.press('Enter');
    await expect.poll(() => runs.search()).toBe('?run=run_2417');
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
  });

  test('covers its whole column, so a short bar is as easy to hit as a tall one', async ({
    page,
  }) => {
    const overview = new OverviewPage(page);
    await overview.open();

    await expect(overview.barLinks).toHaveCount(14);
    const chart = await overview.chart.boundingBox();
    // The failed run's bar is a 4 px stub; its link spans the chart's height.
    const failed = await overview.barLinks.nth(9).boundingBox();
    expect(failed?.height).toBe(chart?.height);
  });

  test('opens the history from "All runs"', async ({ page }) => {
    const overview = new OverviewPage(page);
    const runs = new RunsPage(page);
    await overview.open();

    await expect(overview.allRuns).toHaveAttribute('href', '/runs');
    await overview.allRuns.click();

    await expect.poll(() => runs.search()).toBe('');
    await expect(runs.heading).toHaveText('Runs');
    await expect(page).toHaveURL(/\/runs$/);
  });

  test('opens the run an issue belongs to', async ({ page, api }) => {
    api.on('GET', '/admin/v1/issues', () => ({
      json: issuePage([OPEN_ISSUES[0], openIssue({ id: 'iss_9', run_id: 'run_2416' })]),
    }));
    const overview = new OverviewPage(page);
    const runs = new RunsPage(page);
    await overview.open();

    await expect(overview.issueCards.nth(0)).toHaveAttribute('href', '/runs?run=run_2418');
    await overview.issueCards.nth(1).click();

    await expect.poll(() => runs.search()).toBe('?run=run_2416');
    await expect(runs.detailHeading).toHaveText('Yesterday, 6:00 PM');
  });

  test('links more than 5 open issues to the runs with problems', async ({ page, api }) => {
    const five = Array.from({ length: 5 }, (_, i) => openIssue({ id: `iss_${i}` }));
    api.on('GET', '/admin/v1/issues', () => ({ json: issuePage(five, 7) }));
    const overview = new OverviewPage(page);
    const runs = new RunsPage(page);
    await overview.open();

    await expect(overview.issueCards).toHaveCount(5);
    await expect(overview.problemsLink).toHaveText('7 open');
    await expect(overview.problemsLink).toHaveAttribute('href', '/runs?problems=true');
    await overview.problemsLink.click();

    await expect.poll(() => runs.search()).toBe('?problems=true');
    await expect(runs.withProblems).toHaveAttribute('aria-current', 'true');
  });

  test('keeps 5 or fewer open issues as a count, not a link', async ({ page, api }) => {
    const five = Array.from({ length: 5 }, (_, i) => openIssue({ id: `iss_${i}` }));
    api.on('GET', '/admin/v1/issues', () => ({ json: issuePage(five, 5) }));
    const overview = new OverviewPage(page);
    await overview.open();

    await expect(overview.openCount).toHaveText('5 open');
    await expect(overview.problemsLink).toHaveCount(0);
  });

  test('offers "Go to runs" when the overview did not load', async ({ page, api }) => {
    const down = () => ({ status: 503, json: { status: 503, trace_id: 'req_1' } });
    for (const path of [
      '/admin/v1/overview/status',
      '/admin/v1/runs',
      '/admin/v1/articles',
      '/admin/v1/issues',
    ]) {
      api.on('GET', path, down);
    }
    const overview = new OverviewPage(page);
    await overview.open();

    await expect(overview.pageStateActions).toHaveText(['Try again', 'Go to runs']);
    await expect(overview.pageStateActions.nth(1)).toHaveAttribute('href', '/runs');
  });
});
