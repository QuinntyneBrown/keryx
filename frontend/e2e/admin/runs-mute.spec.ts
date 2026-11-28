// Acceptance Test
// Traces to: L2-020 AC2, AC3; L2-075
// Description: Each open issue in a run's detail offers "Mute". Muted, it shows a "Muted" badge
// and "Unmute" in its place, with focus on it, and a toast says it no longer raises alerts, with
// Undo. Undo or Unmute opens it again. An issue that is over offers neither. Every name is text.

import type { Request } from '@playwright/test';
import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, runDetail, runIssue } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

const COVENANT = '“Source timed out” for Covenant Cycling Collective';
const STAGE_ISSUE = runIssue(
  'iss_4',
  'error',
  'stage_failed',
  null,
  'Classify stopped.',
  1,
  'stage',
);

test.describe('Mute an issue', () => {
  let sent: Request[];

  test.beforeEach(async ({ page, api }) => {
    sent = [];
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', { issues: [...RUN_2418_ISSUES, STAGE_ISSUE] }),
    }));
    for (const action of ['mute', 'unmute'] as const) {
      for (const issue of [...RUN_2418_ISSUES, STAGE_ISSUE]) {
        api.on('POST', `/admin/v1/issues/${issue['id']}/${action}`, (request) => {
          sent.push(request);
          const state = action === 'mute' ? 'muted' : 'open';
          return { json: { ...issue, state, muted: state === 'muted' } };
        });
      }
    }
  });

  test('mutes an issue, says so with Undo, and shows it muted with Unmute', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.issueMutes()).toHaveCount(4);
    await expect(runs.issueMute(COVENANT)).toHaveText('Mute');

    await runs.issueMute(COVENANT).click();

    await expect(runs.toastTitle).toHaveText('Issue muted');
    await expect(runs.toastText).toHaveText(
      '“Source timed out” no longer raises alerts for Covenant Cycling Collective.',
    );
    await expect(runs.toastAction).toHaveText('Undo');
    expect(sent.map((request) => [request.method(), new URL(request.url()).pathname])).toEqual([
      ['POST', '/admin/v1/issues/iss_2/mute'],
    ]);
    expect(sent[0].headers()['x-xsrf-token']).toBe('tok_1');
    await expect(runs.mutedBadges).toHaveText(['Muted']);
    await expect(runs.issues.nth(1).locator('.badge')).toHaveText('Muted');
    await expect(runs.issueMute(COVENANT)).toHaveCount(0);
    await expect(runs.issueUnmute(COVENANT)).toBeFocused();
    await expect(runs.issueUnmute(COVENANT)).toHaveText('Unmute');
  });

  test('opens the issue again on Undo, with focus back on Mute', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();
    await runs.issueMute(COVENANT).click();

    await runs.toastAction.click();

    await expect(runs.mutedBadges).toHaveCount(0);
    await expect(runs.issueMute(COVENANT)).toBeFocused();
    expect(sent.map((request) => new URL(request.url()).pathname)).toEqual([
      '/admin/v1/issues/iss_2/mute',
      '/admin/v1/issues/iss_2/unmute',
    ]);
  });

  test('opens a muted issue again on Unmute, by keyboard', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', {
        issues: [
          RUN_2418_ISSUES[0],
          runIssue(
            'iss_2',
            'error',
            'source_timeout',
            'Covenant Cycling Collective',
            'Timed out.',
            4,
            'source',
            'muted',
          ),
        ],
      }),
    }));
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.mutedBadges).toHaveText(['Muted']);

    await runs.issueUnmute(COVENANT).press('Enter');

    await expect(runs.mutedBadges).toHaveCount(0);
    await expect(runs.issueMute(COVENANT)).toBeFocused();
    expect(sent.map((request) => new URL(request.url()).pathname)).toEqual([
      '/admin/v1/issues/iss_2/unmute',
    ]);
  });

  test('says what a stage’s issue no longer raises, with no source to name', async ({ page }) => {
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueMute('“A stage failed”').click();

    await expect(runs.toastText).toHaveText('“A stage failed” no longer raises alerts.');
  });

  test('offers neither Mute nor Unmute on an issue that is over', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', {
        issues: [
          runIssue(
            'iss_9',
            'error',
            'source_timeout',
            'Old Gym',
            'Timed out.',
            1,
            'source',
            'closed',
          ),
        ],
      }),
    }));
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.issues).toHaveCount(1);
    await expect(runs.issueMutes()).toHaveCount(0);
    await expect(runs.detail.getByRole('button', { name: /^Unmute / })).toHaveCount(0);
  });
});
