// Acceptance Test
// Traces to: L2-059 AC1, AC4, AC5; L2-008 AC5
// Description: A run with failed sources offers "Retry failed sources". Pressing it is busy while
// the one request is out, then says which sources are fetched again and follows the run; a run
// that failed starts a new run, and a retry while another run is in progress says so.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RUN_2418_ISSUES, RUN_HISTORY, runDetail, runningDetail } from '../fixtures/runs';
import { RunsPage } from '../page-objects/admin/runs.page';

test.describe('Retry failed sources', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: runDetail('run_2418', { issues: RUN_2418_ISSUES }),
    }));
  });

  test('is offered for a run with failed sources and not for one that succeeded', async ({
    page,
  }) => {
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');
    await expect(runs.retry).toHaveText('Retry failed sources');
    await expect(runs.retry).toBeEnabled();

    await runs.open('?run=run_2417');
    await expect(runs.detailHeading).toHaveText('Today, 12:00 AM');
    await expect(runs.retry).toHaveCount(0);

    await runs.open('?run=run_2414');
    await expect(runs.retry).toHaveText('Retry failed sources');
  });

  test('fetches the failed sources again and says which', async ({ page, api }) => {
    // Timers wait for the clock: the run reads Running from the read that follows the retry.
    await page.clock.install({ time: OVERVIEW_NOW });
    let posts = 0;
    let retried = false;
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('POST', '/admin/v1/runs/run_2418/retry', async (request) => {
      posts += 1;
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      await held;
      retried = true;
      return { status: 202, json: { id: 'run_2418' } };
    });
    api.on('GET', '/admin/v1/runs', () => {
      const items = retried
        ? [{ ...RUN_HISTORY[0], outcome: 'running' }, ...RUN_HISTORY.slice(1)]
        : RUN_HISTORY;
      return { json: { items, total: 14, page: 1, page_size: 14 } };
    });
    // Retried, the run is running over its 2 failed sources.
    api.on('GET', '/admin/v1/runs/run_2418', () => ({
      json: retried
        ? runDetail('run_2418', { outcome: 'running', sources_checked: 0, sources_total: 2 })
        : // The held-items warning first: only failed sources are named.
          runDetail('run_2418', { issues: [RUN_2418_ISSUES[2], ...RUN_2418_ISSUES.slice(0, 2)] }),
    }));
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await runs.retry.click();

    await expect(runs.retry).toHaveAttribute('aria-busy', 'true');
    release();
    await expect(runs.toastTitle).toHaveText('Retrying 2 failed sources');
    await expect(runs.toastText).toHaveText(
      'Redeemed Run Club Toronto and Covenant Cycling Collective. Results are added to this run.',
    );
    await expect(runs.runNow).toHaveText('Collecting…');
    await expect(runs.detailBadge).toHaveText('Running');
    await expect(runs.toast).toHaveCount(1);
    expect(posts).toBe(1);
  });

  test('starts a new run for a run that failed', async ({ page, api }) => {
    api.on('POST', '/admin/v1/runs/run_2414/retry', () => ({
      status: 202,
      json: { id: 'run_2419' },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({ json: runningDetail() }));
    const runs = new RunsPage(page);
    await runs.open('?run=run_2414');

    await runs.retry.click();

    await expect(runs.toastTitle).toHaveText('Collection started');
    await expect(runs.toastText).toHaveText('The run failed, so a new run checks every source.');
    await expect.poll(() => runs.search()).toBe('?run=run_2419');
    await expect(runs.detailBadge).toHaveText('Running');
  });

  test('says a collection is already running', async ({ page, api }) => {
    api.on('POST', '/admin/v1/runs/run_2418/retry', () => ({
      status: 409,
      json: { status: 409, code: 'run_in_progress', run_id: 'run_2419', trace_id: 'req_2' },
    }));
    api.on('GET', '/admin/v1/runs/run_2419', () => ({ json: runningDetail() }));
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await runs.retry.click();

    await expect(runs.toastTitle).toHaveText('Collection already running');
    await expect(runs.toastText).toHaveText('Retry failed sources once it has finished.');
    await expect(runs.runNow).toHaveText('Collecting…');
    await expect(runs.retry).toBeEnabled();
  });

  test('says the retry did not start, and retries on Try again', async ({ page, api }) => {
    let posts = 0;
    api.on('POST', '/admin/v1/runs/run_2418/retry', () => {
      posts += 1;
      return posts === 1
        ? { status: 503, json: { status: 503, trace_id: 'req_9' } }
        : { status: 202, json: { id: 'run_2418' } };
    });
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await runs.retry.click();

    await expect(runs.toastTitle).toHaveText('Retry didn’t start');
    await expect(runs.toastText).toHaveText(
      'Keryx couldn’t reach its API just now, so no source was fetched again. Reference req_9',
    );
    await expect(runs.retry).toBeEnabled();

    await runs.toastAction.click();

    await expect(runs.toastTitle).toHaveText('Retrying 2 failed sources');
    expect(posts).toBe(2);
  });

  test('follows an older run while its failed sources are fetched again', async ({ page, api }) => {
    let retried = false;
    api.on('POST', '/admin/v1/runs/run_2410/retry', () => {
      retried = true;
      return { status: 202, json: { id: 'run_2410' } };
    });
    api.on('GET', '/admin/v1/runs', () => ({
      json: {
        items: RUN_HISTORY.map((run) =>
          retried && run.id === 'run_2410' ? { ...run, outcome: 'running' } : run,
        ),
        total: 14,
        page: 1,
        page_size: 14,
      },
    }));
    api.on('GET', '/admin/v1/runs/run_2410', () => ({
      json: runDetail('run_2410', retried ? { outcome: 'running', sources_total: 4 } : {}),
    }));
    const runs = new RunsPage(page);
    await runs.open('?run=run_2410');

    await runs.retry.click();

    await expect(runs.detailBadge).toHaveText('Running');
    await expect(runs.runNow).toHaveText('Collecting…');
  });

  test('sends one request for two quick presses', async ({ page, api }) => {
    let posts = 0;
    api.on('POST', '/admin/v1/runs/run_2418/retry', () => {
      posts += 1;
      return { status: 202, json: { id: 'run_2418' } };
    });
    const runs = new RunsPage(page);
    await runs.open('?run=run_2418');

    await runs.retry.dblclick();

    await expect(runs.toastTitle).toHaveText('Retrying 2 failed sources');
    expect(posts).toBe(1);
  });
});
