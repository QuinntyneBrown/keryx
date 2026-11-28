// Acceptance Test
// Traces to: L2-057 AC4, AC5, AC6
// Description: The chosen run's issues: each with its severity as a word, its title and
// subject, its detail as text and a streak where it applies; a run with none says so.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, runDetail, runIssue } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Run issues', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('lists the issues the run raised', { tag: '@critical' }, async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', { issues: RUN_2418_ISSUES }),
    }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.issuesHeading).toHaveText('Issues in this run');
    await expect(runs.issueTitles).toHaveText([
      'Error: Feed returned 404 · Redeemed Run Club Toronto',
      'Error: Source timed out · Covenant Cycling Collective',
      'Warning: Items held for review · Google News · “Toronto church fitness”',
    ]);
    await expect(runs.issueWords).toHaveText(['Error:', 'Error:', 'Warning:']);
    await expect(runs.issueSubjects).toHaveText([
      '· Redeemed Run Club Toronto',
      '· Covenant Cycling Collective',
      '· Google News · “Toronto church fitness”',
    ]);
    await expect(runs.issueDetails).toHaveText([
      'GET https://redeemedrun.ca/feed → 404 Not Found. The site may have moved its blog.',
      'Events page did not respond within 30s on 3 retries.',
      'Relevance score between 0.45 and 0.60 — may not be Christian fitness content.',
    ]);
    expect(await runs.issueStreaks()).toEqual([null, 'Failing 4 runs in a row', null]);
    expect(await runs.issueTones()).toEqual(['danger', 'danger', 'warning']);
    await expect(runs.noIssues).toHaveCount(0);
  });

  test('says a run raised no issues', async ({ page }) => {
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.issuesHeading).toHaveText('Issues in this run');
    await expect(runs.issues).toHaveCount(0);
    await expect(runs.noIssues.getByRole('heading', { level: 4 })).toHaveText(
      'No issues in this run',
    );
    await expect(runs.noIssues).toContainText(
      'Every source responded and nothing was held for review.',
    );
  });

  // A stage's issue has no source; its detail, from the run, is shown as text.
  test('shows a stage issue without a subject, and its detail as text', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', {
        issues: [
          runIssue('iss_9', 'error', 'stage_failed', null, '<b>Store</b> failed', 2, 'stage'),
        ],
      }),
    }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.issueTitles).toHaveText(['Error: A stage failed']);
    await expect(runs.issueSubjects).toHaveCount(0);
    await expect(runs.issueDetails).toHaveText(['<b>Store</b> failed']);
    expect(await runs.issueStreaks()).toEqual(['Failing 2 runs in a row']);
  });
});
