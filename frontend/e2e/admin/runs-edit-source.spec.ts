// Acceptance Test
// Traces to: L2-057 AC4; L2-065 AC1, AC2, AC6
// Description: Each issue about a source offers "Edit source", which opens the same dialog as
// Sources for that source; an issue about a stage offers none. Saved, the run is read again and
// focus returns to the button. A source removed meanwhile is said instead.

import { expect, test } from '../fixtures/test';
import { REGIONS } from '../fixtures/articles';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, runDetail, runIssue } from '../fixtures/runs';
import { EditSourceDialog } from '../page-objects/admin/edit-source.dialog';
import { RunsPage } from '../page-objects/admin/runs.page';

const DETAIL = {
  id: 'src_iss_1',
  kind: 'rss',
  address: 'https://redeemedrun.ca/feed',
  organization_name: 'Redeemed Run Club Toronto',
  region: 'toronto',
  health: 'failing',
  failure_reason: 'not_found',
  consecutive_failures: 1,
  article_count: 31,
  created_at: '2026-11-01T12:00:00Z',
  last_fetched_at: '2026-11-28T11:01:00Z',
  last_succeeded_at: null,
};

const STAGE_ISSUE = runIssue(
  'iss_4',
  'error',
  'stage_failed',
  null,
  'Classify stopped after 2 attempts.',
  1,
  'stage',
);

test.describe('Edit source from a run', () => {
  let details: number;

  test.beforeEach(async ({ page, api }) => {
    details = 0;
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/runs/run_2418', () => {
      details += 1;
      return { json: runDetail('run_2418', { issues: [...RUN_2418_ISSUES, STAGE_ISSUE] }) };
    });
  });

  test('opens the source an issue is about, and saves it', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources/src_iss_1', () => ({ json: DETAIL }));
    api.on('PUT', '/admin/v1/sources/src_iss_1', (request) => {
      expect(request.postDataJSON()).toEqual({
        address: 'https://redeemedrun.ca/blog/feed',
        organization_name: 'Redeemed Run Club Toronto',
        region: 'toronto',
      });
      return {
        json: {
          source: { ...DETAIL, address: 'https://redeemedrun.ca/blog/feed' },
          test: { reachable: true, reason: null, items_found: 12, items_relevant: 3 },
        },
      };
    });
    const runs = new RunsPage(page);
    await runs.open();

    await expect(runs.issueEdits()).toHaveCount(3);
    const edit = runs.issueEdit('Redeemed Run Club Toronto');
    await expect(edit).toHaveText('Edit source');

    await edit.click();
    const dialog = new EditSourceDialog(page);

    await expect(dialog.title).toHaveText('Edit source');
    await expect(dialog.url).toHaveValue('https://redeemedrun.ca/feed');
    await expect(dialog.organisation).toHaveValue('Redeemed Run Club Toronto');
    await expect(dialog.region).toHaveValue('toronto');
    await expect(dialog.lastFetchTitle).toHaveText('Last fetch failed today at 6:01 AM');

    await dialog.url.fill('https://redeemedrun.ca/blog/feed');
    await dialog.submit.click();

    await expect(dialog.root).toHaveCount(0);
    await expect(runs.toastTitle).toHaveText('Source saved');
    await expect(edit).toBeFocused();
    await expect.poll(() => details).toBe(2);
  });

  test('closes without a change and returns focus to the issue', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources/src_iss_1', () => ({ json: DETAIL }));
    const runs = new RunsPage(page);
    await runs.open();
    const edit = runs.issueEdit('Redeemed Run Club Toronto');

    await edit.click();
    await new EditSourceDialog(page).cancel.click();

    await expect(edit).toBeFocused();
    expect(details).toBe(1);
  });

  test('says when the source was removed meanwhile', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources/src_iss_1', () => ({
      status: 404,
      json: { status: 404, code: 'source_not_found', trace_id: 'req_8' },
    }));
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueEdit('Redeemed Run Club Toronto').click();

    await expect(runs.toastTitle).toHaveText('Source not found');
    await expect(runs.toastText).toHaveText('Redeemed Run Club Toronto was removed meanwhile.');
    await expect(new EditSourceDialog(page).root).toHaveCount(0);
  });

  test('says when the source could not be read, and tries again', async ({ page, api }) => {
    let reads = 0;
    api.on('GET', '/admin/v1/sources/src_iss_1', () => {
      reads += 1;
      return reads === 1
        ? { status: 503, json: { status: 503, trace_id: 'req_6' } }
        : { json: DETAIL };
    });
    const runs = new RunsPage(page);
    await runs.open();

    await runs.issueEdit('Redeemed Run Club Toronto').click();

    await expect(runs.toastTitle).toHaveText('Keryx couldn’t open the source');
    await expect(runs.toastText).toHaveText('Try again in a moment. Reference req_6');

    await runs.toastAction.click();

    await expect(new EditSourceDialog(page).title).toHaveText('Edit source');
  });
});
