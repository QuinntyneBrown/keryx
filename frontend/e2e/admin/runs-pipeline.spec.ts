// Acceptance Test
// Traces to: L2-057 AC2, AC6
// Description: The chosen run's six pipeline stages, in order, each with its status as words,
// its duration and its summary; every summary is text.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_STAGES, runDetail, stage } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Run pipeline', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('shows the six stages in order', { tag: '@critical' }, async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      // Served out of order, the stages still read in the pipeline's order.
      json: runDetail('run_2418', { stages: [...RUN_2418_STAGES].reverse() }),
    }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.pipelineHeading).toHaveText('Pipeline');
    await expect(runs.stepNames).toHaveText([
      'Done with warnings: Fetch sources',
      'Done: Parse & extract',
      'Done with warnings: Classify relevance',
      'Done: Deduplicate',
      'Done: Store',
      'Done: Publish to API',
    ]);
    await expect(runs.stepWords).toHaveText([
      'Done with warnings:',
      'Done:',
      'Done with warnings:',
      'Done:',
      'Done:',
      'Done:',
    ]);
    await expect(runs.stepTimes).toHaveText(['2:48', '0:21', '0:38', '0:06', '0:03', '0:01']);
    await expect(runs.stepDetails).toHaveText([
      '62 of 64 sources fetched · 2 failed',
      '189 candidate items from feeds, pages and posts',
      '53 relevant · 6 held for review · 130 off-topic',
      '18 duplicates merged with existing articles',
      'Saved to articles table',
      '47 new articles available at /v1/articles',
    ]);
    expect(await runs.stepStates()).toEqual(['warn', 'ok', 'warn', 'ok', 'ok', 'ok']);
  });

  test('summarizes a run where nothing failed or was held', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', {
        stages: [
          stage('fetch', 'done', 615, { sources_ok: 64, sources_failed: 0 }),
          stage('parse', 'done', 21, { items: 1 }),
          stage('classify', 'done', 38, { relevant: 1, held: 0, off_topic: 0 }),
          stage('deduplicate', 'done', 6, { duplicates: 1, rejected: 2 }),
          stage('store', 'done', 3, { added: 1, updated: 0, unchanged: 0 }),
          stage('publish', 'done', 1, { published: 1 }),
        ],
      }),
    }));
    const runs = new RunsPage(page);

    await runs.open();

    await expect(runs.stepTimes.first()).toHaveText('10:15');
    await expect(runs.stepDetails).toHaveText([
      '64 of 64 sources fetched',
      '1 candidate item from feeds, pages and posts',
      '1 relevant · 0 held for review · 0 off-topic',
      '1 duplicate merged with existing articles · 2 rejected',
      'Saved to articles table',
      '1 new article available at /v1/articles',
    ]);
  });
});
