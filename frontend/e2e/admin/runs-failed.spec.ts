// Acceptance Test
// Traces to: L2-057 AC3, AC6
// Description: A failed run's detail: the stage that failed reads "Failed" with the generic reason
// the run recorded, and the stages after it read "Not run" and "Skipped", publish saying the API
// still serves the previous run.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { runDetail, stage } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Failed run', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('shows the stage that failed and those it stopped', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2414', () => ({
      json: runDetail('run_2414', {
        failure_reason: 'The records of this run could not be saved.',
        stages: [
          stage('fetch', 'done', 41, { sources_ok: 64, sources_failed: 0 }),
          stage('parse', 'done', 5, { items: 212 }),
          stage('classify', 'failed', 2, {}, 'The relevance step could not finish.'),
          stage('deduplicate', 'not_run', 0, {}),
          stage('store', 'not_run', 0, {}),
          stage('publish', 'not_run', 0, {}),
        ],
      }),
    }));
    const runs = new RunsPage(page);

    await runs.open('?run=run_2414');

    await expect(runs.detailBadge).toHaveText('Failed');
    await expect(runs.stepNames).toHaveText([
      'Done: Fetch sources',
      'Done: Parse & extract',
      'Failed: Classify relevance',
      'Not run: Deduplicate',
      'Not run: Store',
      'Not run: Publish to API',
    ]);
    await expect(runs.stepTimes).toHaveText(['0:41', '0:05', '0:02', '—', '—', '—']);
    await expect(runs.stepDetails).toHaveText([
      '64 of 64 sources fetched',
      '212 candidate items from feeds, pages and posts',
      'The relevance step could not finish.',
      'Skipped',
      'Skipped',
      'Skipped — API still serving previous run',
    ]);
    expect(await runs.stepStates()).toEqual([
      'ok',
      'ok',
      'failed',
      'skipped',
      'skipped',
      'skipped',
    ]);
  });

  // A failed stage whose run recorded no reason still says it failed, never blank.
  test('says a stage failed when no reason was recorded', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2414', () => ({
      json: runDetail('run_2414', {
        stages: [
          stage('fetch', 'done', 41, { sources_ok: 64, sources_failed: 0 }),
          stage('parse', 'done', 5, { items: 212 }),
          stage('classify', 'done', 4, { relevant: 50, held: 0, off_topic: 162 }),
          stage('deduplicate', 'done', 1, { duplicates: 3, rejected: 0 }),
          stage('store', 'failed', 2, {}),
          stage('publish', 'not_run', 0, {}),
        ],
      }),
    }));
    const runs = new RunsPage(page);

    await runs.open('?run=run_2414');

    await expect(runs.stepDetails.nth(4)).toHaveText('This stage failed.');
    await expect(runs.stepDetails.nth(5)).toHaveText('Skipped — API still serving previous run');
  });
});
