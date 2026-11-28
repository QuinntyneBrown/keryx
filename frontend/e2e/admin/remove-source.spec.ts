// Acceptance Test
// Traces to: L2-065 AC3–AC6; L2-063 AC1
// Description: "Remove source" in a row's menu asks first, naming the source and how many
// articles stay, with focus on Cancel. Confirmed, the row goes, the counts follow and a
// notification offers Undo, which adds the source back. A failed removal keeps the source and
// says so in the dialog and in a notification that can try again. Closing changes nothing.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { RemoveSourceDialog } from '../page-objects/admin/remove-source.dialog';
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
  articles_30d: 4,
  last_fetched_at: '2026-11-28T11:01:00Z',
};

const CYCLING = {
  ...RUN_CLUB,
  id: 'src_cccccccccccc',
  address: 'https://covenantcycling.ca/rides',
  organization_name: 'Covenant Cycling',
  health: 'healthy',
  failure_reason: null,
};

const DETAIL = {
  ...RUN_CLUB,
  article_count: 31,
  created_at: '2026-11-01T12:00:00Z',
  last_succeeded_at: null,
};

test.describe('Remove source', () => {
  let removed: boolean;
  let lists: number;

  test.beforeEach(async ({ page, api }) => {
    removed = false;
    lists = 0;
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/sources/src_aaaaaaaaaaaa', () => ({ json: DETAIL }));
    api.on('GET', '/admin/v1/sources', () => {
      lists += 1;
      const items = removed ? [CYCLING] : [RUN_CLUB, CYCLING];
      return {
        json: {
          items,
          total: items.length,
          page: 1,
          page_size: 12,
          all_count: items.length,
          failing_count: removed ? 0 : 1,
          needs_look_count: 0,
        },
      };
    });
  });

  async function openRemove(page: import('@playwright/test').Page) {
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();
    await sources.menuItem('Remove source').click();
    const dialog = new RemoveSourceDialog(page);
    await expect(dialog.title).toHaveText('Remove Redeemed Run Club Toronto?');
    return { sources, dialog };
  }

  test('asks first, naming what stays, with focus on Cancel', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    await expect(sources.menuItems.last()).toHaveText('Remove source');
    await expect(sources.menuItems.last()).toHaveClass(/menu__item--danger/);

    await sources.menuItems.last().click();
    const dialog = new RemoveSourceDialog(page);

    await expect(dialog.title).toHaveText('Remove Redeemed Run Club Toronto?');
    await expect(dialog.description).toHaveText('Keryx stops checking redeemedrun.ca/feed.');
    await expect(dialog.text).toHaveText(
      'The 31 articles already collected from this source stay in the database and in the API, each with its link back to the source. You can add the source again later.',
    );
    await expect(dialog.cancel).toBeFocused();
    await expect(dialog.confirm).toHaveText('Remove source');
  });

  test('removes the source, then Undo adds it back', async ({ page, api }) => {
    let deletes = 0;
    let release: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (release = resolve));
    api.on('DELETE', '/admin/v1/sources/src_aaaaaaaaaaaa', async (request) => {
      deletes += 1;
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      await answered;
      removed = true;
      return { status: 204 };
    });
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/restore', (request) => {
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      removed = false;
      return { json: DETAIL };
    });
    const { sources, dialog } = await openRemove(page);
    await expect(sources.description).toHaveText('2 sources across the GTA · 1 failing');

    await dialog.confirm.click();

    await expect(dialog.confirm).toHaveText('Removing…');
    await page.keyboard.press('Escape');
    await expect(dialog.title).toBeVisible();

    release();

    await expect(dialog.root).toHaveCount(0);
    await expect(sources.rows).toHaveCount(1);
    await expect(sources.row(0).name).toHaveText('Covenant Cycling');
    await expect(sources.description).toHaveText('1 source across the GTA');
    await expect(sources.caption).toBeFocused();
    await expect(sources.toastTitle).toHaveText('Source removed');
    await expect(sources.toastText).toHaveText(
      'Redeemed Run Club Toronto is no longer checked. Its articles stay in Keryx.',
    );
    expect(deletes).toBe(1);

    await sources.toastAction.click();

    await expect(sources.rows).toHaveCount(2);
    await expect(sources.row(0).name).toHaveText('Redeemed Run Club Toronto');
  });

  test('says when Undo cannot add the source back', async ({ page, api }) => {
    api.on('DELETE', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      removed = true;
      return { status: 204 };
    });
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/restore', () => ({
      status: 409,
      json: { status: 409, code: 'source_exists', trace_id: 'req_3' },
    }));
    const { sources, dialog } = await openRemove(page);
    await dialog.confirm.click();
    await expect(sources.toastTitle).toHaveText('Source removed');

    await sources.toastAction.click();

    await expect(sources.toastTitle).toHaveText('Keryx couldn’t add the source back');
    await expect(sources.toastText).toHaveText(
      'Redeemed Run Club Toronto was added again meanwhile, so it is listed already.',
    );
  });

  test('tries Undo again when it fails for another reason', async ({ page, api }) => {
    let restores = 0;
    api.on('DELETE', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      removed = true;
      return { status: 204 };
    });
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/restore', () => {
      restores += 1;
      if (restores === 1) return { status: 503, json: { status: 503, trace_id: 'req_4' } };
      removed = false;
      return { json: DETAIL };
    });
    const { sources, dialog } = await openRemove(page);
    await dialog.confirm.click();
    await sources.toastAction.click();

    await expect(sources.toastTitle).toHaveText('Keryx couldn’t add the source back');
    await expect(sources.toastText).toHaveText('Nothing changed. Try again in a moment.');

    await sources.toastAction.click();

    await expect(sources.rows).toHaveCount(2);
    expect(restores).toBe(2);
  });

  test('keeps the source when the removal fails, and tries again', async ({ page, api }) => {
    let deletes = 0;
    api.on('DELETE', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      deletes += 1;
      if (deletes === 1) return { status: 503, json: { status: 503, trace_id: 'req_0d4f8e63' } };
      removed = true;
      return { status: 204 };
    });
    const { sources, dialog } = await openRemove(page);

    await dialog.confirm.click();

    await expect(dialog.failure).toContainText('Keryx couldn’t remove the source');
    await expect(dialog.failure).toContainText('Nothing changed. Try again in a moment.');
    await expect(dialog.failure).toContainText('Reference req_0d4f8e63');
    await expect(dialog.confirm).toHaveText('Remove source');
    await expect(sources.toastTitle).toHaveText('Keryx couldn’t remove the source');
    await expect(sources.toastText).toHaveText('Redeemed Run Club Toronto is still being checked.');
    await expect(sources.toast).toHaveClass(/toast--danger/);

    await dialog.cancel.click();

    await expect(sources.rows).toHaveCount(2);

    await sources.toastAction.click();

    await expect(sources.toastTitle).toHaveText('Source removed');
    await expect(sources.rows).toHaveCount(1);
    expect(deletes).toBe(2);
  });

  test('closes without a change and returns focus to the row', async ({ page, api }) => {
    let deletes = 0;
    api.on('DELETE', '/admin/v1/sources/src_aaaaaaaaaaaa', () => {
      deletes += 1;
      return { status: 204 };
    });
    const { sources, dialog } = await openRemove(page);

    await page.keyboard.press('Escape');

    await expect(dialog.root).toHaveCount(0);
    await expect(sources.row(0).actions).toBeFocused();
    expect(deletes).toBe(0);
    expect(lists).toBe(1);
  });

  test('says when the source was removed meanwhile', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources/src_aaaaaaaaaaaa', () => ({
      status: 404,
      json: { status: 404, code: 'source_not_found', trace_id: 'req_8' },
    }));
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    await sources.menuItem('Remove source').click();

    await expect(sources.toastTitle).toHaveText('Source not found');
    await expect(new RemoveSourceDialog(page).root).toHaveCount(0);
    await expect.poll(() => lists).toBe(2);
  });
});
