// Acceptance Test
// Traces to: L2-065 AC1, AC2, AC6; L2-063 AC1
// Description: "Edit source" in a row's menu opens a dialog holding the source's current values,
// with the last failed fetch above them. Saving tests the address again: it follows the busy,
// invalid and failed behaviour of adding a source, says how the test went, and the list shows
// the saved values. Closing it changes nothing and returns focus to the row's menu button.

import { expect, test } from '../fixtures/test';
import { REGIONS } from '../fixtures/articles';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { EditSourceDialog } from '../page-objects/admin/edit-source.dialog';
import { SourcesPage } from '../page-objects/admin/sources.page';

const RUN_CLUB = {
  id: 'src_aaaaaaaaaaaa',
  kind: 'rss',
  address: 'https://redeemedrun.ca/feed',
  organization_name: 'Redeemed Run Club Toronto',
  region: 'toronto',
  region_name: 'Toronto',
  health: 'failing',
  failure_reason: 'not_found',
  consecutive_failures: 1,
  held_count: 0,
  articles_30d: 0,
  last_fetched_at: '2026-11-28T11:01:00Z',
};

const QUERY = {
  ...RUN_CLUB,
  id: 'src_bbbbbbbbbbbb',
  kind: 'news_query',
  address: '"church" AND fitness near:Toronto',
  organization_name: 'Toronto church fitness',
  health: 'healthy',
  failure_reason: null,
};

const page1 = (items: unknown[]) => ({
  json: {
    items,
    total: items.length,
    page: 1,
    page_size: 12,
    all_count: items.length,
    failing_count: 1,
    needs_look_count: 0,
  },
});

test.describe('Edit source', () => {
  let lists: number;
  let saved: typeof RUN_CLUB | null;

  test.beforeEach(async ({ page, api }) => {
    lists = 0;
    saved = null;
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/sources', () => {
      lists += 1;
      return page1([saved ?? RUN_CLUB, QUERY]);
    });
  });

  async function openEdit(page: import('@playwright/test').Page, row = 0) {
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(row).actions.click();
    await sources.menuItem('Edit source').click();
    const dialog = new EditSourceDialog(page);
    await expect(dialog.title).toHaveText('Edit source');
    return { sources, dialog };
  }

  test('opens on the source as it is, with its last failed fetch', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    await expect(sources.menuItems).toHaveText([
      'Edit source',
      'Test fetch now',
      'Open redeemedrun.ca (opens in a new tab)',
      'Remove source',
    ]);

    await sources.menuItem('Edit source').click();
    const dialog = new EditSourceDialog(page);

    await expect(dialog.title).toHaveText('Edit source');
    await expect(dialog.description).toHaveText('Changes apply from the next scheduled run.');
    await expect(dialog.lastFetchTitle).toHaveText('Last fetch failed today at 6:01 AM');
    await expect(dialog.lastFetch).toContainText(
      'GET https://redeemedrun.ca/feed → 404 Not Found. Check the address below.',
    );
    await expect(dialog.types).toHaveText(['RSS']);
    await expect(dialog.url).toHaveValue('https://redeemedrun.ca/feed');
    await expect(dialog.url).toBeFocused();
    await expect(dialog.organisation).toHaveValue('Redeemed Run Club Toronto');
    await expect(dialog.region).toHaveValue('toronto');
    await expect(dialog.hint).toHaveText('Keryx tests the address again when you save.');
    await expect(dialog.submit).toHaveText('Save source');
  });

  test('saves the changes, says how the test went and lists the saved values', async ({
    page,
    api,
  }) => {
    let puts = 0;
    let release: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (release = resolve));
    api.on('PUT', '/admin/v1/sources/src_aaaaaaaaaaaa', async (request) => {
      puts += 1;
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      expect(request.postDataJSON()).toEqual({
        address: 'https://redeemedrun.ca/blog/feed',
        organization_name: 'Redeemed Run Club',
        region: 'gta',
      });
      await answered;
      saved = {
        ...RUN_CLUB,
        address: 'https://redeemedrun.ca/blog/feed',
        organization_name: 'Redeemed Run Club',
        region: 'gta',
        region_name: 'GTA',
      };
      return {
        json: {
          source: { ...saved, created_at: '2026-11-01T12:00:00Z', last_succeeded_at: null },
          test: { reachable: true, reason: null, items_found: 12, items_relevant: 3 },
        },
      };
    });
    const { sources, dialog } = await openEdit(page);

    await dialog.url.fill('https://redeemedrun.ca/blog/feed');
    await dialog.organisation.fill('Redeemed Run Club');
    await dialog.region.selectOption('gta');
    await dialog.submit.click();

    await expect(dialog.submit).toHaveText('Saving…');
    await expect(dialog.url).toBeDisabled();
    await page.keyboard.press('Escape');
    await expect(dialog.title).toBeVisible();

    release();

    await expect(dialog.root).toHaveCount(0);
    await expect(sources.toastTitle).toHaveText('Source saved');
    await expect(sources.toastText).toHaveText(
      'Test fetch found 12 items — 3 look relevant. Changes apply from the next scheduled run.',
    );
    await expect(sources.row(0).name).toHaveText('Redeemed Run Club');
    await expect(sources.row(0).region).toHaveText('GTA');
    await expect(sources.row(0).actions).toBeFocused();
    expect(puts).toBe(1);
    expect(lists).toBe(2);
  });

  test('shows the list’s error state when it cannot be read after a save', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/sources', () => {
      lists += 1;
      return lists === 1
        ? page1([RUN_CLUB, QUERY])
        : { status: 503, json: { status: 503, trace_id: 'req_7' } };
    });
    api.on('PUT', '/admin/v1/sources/src_aaaaaaaaaaaa', () => ({
      json: {
        source: { ...RUN_CLUB, created_at: '2026-11-01T12:00:00Z', last_succeeded_at: null },
        test: { reachable: true, reason: null, items_found: 1, items_relevant: 1 },
      },
    }));
    const { sources, dialog } = await openEdit(page);

    await dialog.submit.click();

    await expect(sources.toastTitle).toHaveText('Source saved');
    await expect(sources.emptyTitle).toHaveText('Sources didn’t load');
    expect(lists).toBe(3);
  });

  test('says what is wrong and sends nothing until it is right', async ({ page, api }) => {
    let puts = 0;
    api.on('PUT', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      puts += 1;
      return { status: 409, json: { status: 409, code: 'source_exists', trace_id: 'req_2' } };
    });
    const { dialog } = await openEdit(page);

    await dialog.url.fill('');
    await dialog.submit.click();

    await expect(dialog.urlError).toHaveText(
      'Enter a full URL starting with https://, like https://lifepointfit.ca/events.',
    );
    await expect(dialog.url).toBeFocused();
    expect(puts).toBe(0);

    await dialog.url.fill('https://redeemedrun.ca/other');
    await dialog.submit.click();

    await expect(dialog.urlError).toHaveText('This source is already in the list.');
    await expect(dialog.url).toBeFocused();
    expect(puts).toBe(1);
  });

  test('keeps the entries and gives a reference when the save fails', async ({ page, api }) => {
    api.on('PUT', '/admin/v1/sources/src_aaaaaaaaaaaa', () => ({
      status: 503,
      json: { status: 503, trace_id: 'req_5be2c7a1' },
    }));
    const { dialog } = await openEdit(page);

    await dialog.organisation.fill('Redeemed Run Club');
    await dialog.submit.click();

    await expect(dialog.failure).toContainText('Keryx couldn’t save your changes');
    await expect(dialog.failure).toContainText('Reference req_5be2c7a1');
    await expect(dialog.organisation).toHaveValue('Redeemed Run Club');
    await expect(dialog.submit).toHaveText('Save source');
  });

  test('closes without a change and returns focus to the row', async ({ page, api }) => {
    let puts = 0;
    api.on('PUT', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      puts += 1;
      return { status: 500, json: {} };
    });
    const { sources, dialog } = await openEdit(page);

    await dialog.organisation.fill('Something else');
    await page.keyboard.press('Escape');

    await expect(dialog.root).toHaveCount(0);
    await expect(sources.row(0).actions).toBeFocused();
    await expect(sources.row(0).name).toHaveText('Redeemed Run Club Toronto');

    await sources.row(0).actions.click();
    await sources.menuItem('Edit source').click();
    await dialog.cancel.click();

    await expect(dialog.root).toHaveCount(0);
    await expect(sources.row(0).actions).toBeFocused();
    expect(puts).toBe(0);
    expect(lists).toBe(1);
  });

  test('edits a news query by its query text, with no failed fetch to show', async ({
    page,
    api,
  }) => {
    let body: unknown = null;
    api.on('PUT', '/admin/v1/sources/src_bbbbbbbbbbbb', (request) => {
      body = request.postDataJSON();
      return {
        json: {
          source: { ...QUERY, created_at: '2026-11-01T12:00:00Z', last_succeeded_at: null },
          test: { reachable: true, reason: null, items_found: 4, items_relevant: 0 },
        },
      };
    });
    const { sources, dialog } = await openEdit(page, 1);

    await expect(dialog.lastFetch).toHaveCount(0);
    await expect(dialog.types).toHaveText(['News query']);
    await expect(dialog.urlLabel).toHaveText('Query');

    await dialog.url.fill('"church" AND run near:Toronto');
    await dialog.submit.click();

    await expect(sources.toastTitle).toHaveText('Source saved');
    await expect(sources.toastText).toHaveText(
      'Test fetch found 4 articles — none look relevant. Changes apply from the next scheduled run.',
    );
    await expect(sources.toast).toHaveClass(/toast--warning/);
    expect(body).toEqual({
      address: '"church" AND run near:Toronto',
      organization_name: 'Toronto church fitness',
      region: 'toronto',
    });
  });
});
