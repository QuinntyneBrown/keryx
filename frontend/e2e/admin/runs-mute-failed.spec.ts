// Acceptance Test
// Traces to: L2-020 AC2, AC3; L2-075; L2-083
// Description: A mute or unmute the API does not take leaves the issue as it was and says so with
// one generic message, offering "Try again". An issue that is over (409 issue_closed) or gone
// (404 issue_not_found) gets the same message; a 409 also reads the run again, so the issue shows
// as it now is.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, runDetail, runIssue } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

const COVENANT = '“Source timed out” for Covenant Cycling Collective';
const NOT_MUTED =
  'Keryx couldn’t reach its API just now, so the issue still raises alerts. Try again.';
const COVENANT_ISSUE = RUN_2418_ISSUES[1];

test.describe('Mute an issue that fails', () => {
  let details: number;
  let attempts: number;

  test.beforeEach(async ({ page, api }) => {
    details = 0;
    attempts = 0;
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/runs/run_2418', () => {
      details += 1;
      return { json: runDetail('run_2418', { issues: RUN_2418_ISSUES }) };
    });
  });

  test('says the issue was not muted and mutes it on Try again', async ({ page, api }) => {
    api.on('POST', '/admin/v1/issues/iss_2/mute', () => {
      attempts += 1;
      return attempts === 1
        ? { status: 500, json: { status: 500, trace_id: 'req_7' } }
        : { json: { ...COVENANT_ISSUE, state: 'muted', muted: true } };
    });
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueMute(COVENANT).click();

    await expect(runs.toastTitle).toHaveText('Issue not muted');
    await expect(runs.toastText).toHaveText(NOT_MUTED);
    await expect(runs.mutedBadges).toHaveCount(0);
    await expect(runs.issueMute(COVENANT)).toBeEnabled();

    await runs.toastAction.click();

    await expect(runs.mutedBadges).toHaveText(['Muted']);
    await expect(runs.issueUnmute(COVENANT)).toBeFocused();
    expect(attempts).toBe(2);
  });

  test('says the issue was not unmuted, still muted', async ({ page, api }) => {
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', {
        issues: [
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
    api.on('POST', '/admin/v1/issues/iss_2/unmute', () => ({
      status: 503,
      json: { status: 503, trace_id: 'req_8' },
    }));
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueUnmute(COVENANT).click();

    await expect(runs.toastTitle).toHaveText('Issue not unmuted');
    await expect(runs.toastText).toHaveText(
      'Keryx couldn’t reach its API just now, so the issue is still muted. Try again.',
    );
    await expect(runs.toastAction).toHaveText('Try again');
    await expect(runs.mutedBadges).toHaveText(['Muted']);
  });

  test('reads the run again when the issue is over, which then offers no Mute', async ({
    page,
    api,
  }) => {
    api.on('POST', '/admin/v1/issues/iss_2/mute', () => {
      api.on('GET', '/admin/v1/runs/run_2418', () => {
        details += 1;
        return {
          json: runDetail('run_2418', {
            issues: [
              RUN_2418_ISSUES[0],
              { ...COVENANT_ISSUE, state: 'closed' },
              RUN_2418_ISSUES[2],
            ],
          }),
        };
      });
      return { status: 409, json: { status: 409, code: 'issue_closed', trace_id: 'req_9' } };
    });
    const runs = new RunsPage(page);
    await runs.open();
    await expect(runs.issueMutes()).toHaveCount(3);

    await runs.issueMute(COVENANT).click();

    await expect(runs.toastTitle).toHaveText('Issue not muted');
    await expect(runs.toastText).toHaveText(NOT_MUTED);
    await expect(runs.issueMutes()).toHaveCount(2);
    await expect(runs.issueMute(COVENANT)).toHaveCount(0);
    expect(details).toBe(2);
  });

  test('gives the same message for an issue that is gone', async ({ page, api }) => {
    api.on('POST', '/admin/v1/issues/iss_2/mute', () => ({
      status: 404,
      json: { status: 404, code: 'issue_not_found', trace_id: 'req_10' },
    }));
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueMute(COVENANT).click();

    await expect(runs.toastTitle).toHaveText('Issue not muted');
    await expect(runs.toastText).toHaveText(NOT_MUTED);
    expect(details).toBe(1);
  });
});
