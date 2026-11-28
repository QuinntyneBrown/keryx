// Acceptance Test
// Traces to: L2-063 AC1–AC5
// Description: Each source row has an "Actions for <name>" menu button that follows the WAI-ARIA
// menu pattern: it opens with focus on its first item, the arrow keys, Home and End move and
// wrap, Escape returns focus to the button, and one menu is open at a time. "Test fetch now" tests
// the source and reports the result, leaving the list as it was; "Open <host>" opens an http(s)
// address in a new tab.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { SourcesPage } from '../page-objects/admin/sources.page';

const source = (id: string, name: string, fields: Record<string, unknown> = {}) => ({
  id,
  kind: 'rss',
  address: 'https://redeemedrun.ca/feed',
  organization_name: name,
  region: 'toronto',
  region_name: 'Toronto',
  health: 'failing',
  failure_reason: 'not_found',
  consecutive_failures: 1,
  held_count: 0,
  articles_30d: 0,
  last_fetched_at: '2026-11-28T11:01:00Z',
  ...fields,
});

const SOURCES = [
  source('src_aaaaaaaaaaaa', 'Redeemed Run Club Toronto'),
  source('src_bbbbbbbbbbbb', 'Toronto church fitness', {
    kind: 'news_query',
    address: '"church" AND fitness near:Toronto',
    health: 'healthy',
    failure_reason: null,
  }),
  source('src_cccccccccccc', 'Odd Address Club', {
    address: 'javascript:alert(1)',
    health: 'healthy',
    failure_reason: null,
  }),
];

test.describe('Source row menu', () => {
  let lists: number;

  test.beforeEach(async ({ page, api }) => {
    lists = 0;
    await page.clock.setFixedTime(OVERVIEW_NOW);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/sources', () => {
      lists += 1;
      return {
        json: {
          items: SOURCES,
          total: 3,
          page: 1,
          page_size: 12,
          all_count: 3,
          failing_count: 1,
          needs_look_count: 0,
        },
      };
    });
  });

  test('opens on its first item and moves, wraps and closes by key', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open();
    const button = sources.row(0).actions;
    await expect(button).toHaveAccessibleName('Actions for Redeemed Run Club Toronto');
    await expect(button).toHaveAttribute('aria-haspopup', 'menu');
    await expect(button).toHaveAttribute('aria-expanded', 'false');

    await button.click();

    await expect(sources.menu).toHaveAccessibleName('Actions for Redeemed Run Club Toronto');
    await expect(sources.menuItems).toHaveText([
      'Edit source',
      'Test fetch now',
      'Open redeemedrun.ca (opens in a new tab)',
      'Remove source',
    ]);
    await expect(button).toHaveAttribute('aria-expanded', 'true');
    await expect(sources.menuItems.nth(0)).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(sources.menuItems.nth(1)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(sources.menuItems.nth(2)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(sources.menuItems.nth(3)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(sources.menuItems.nth(0)).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(sources.menuItems.nth(3)).toBeFocused();
    await page.keyboard.press('Home');
    await expect(sources.menuItems.nth(0)).toBeFocused();
    await page.keyboard.press('End');
    await expect(sources.menuItems.nth(3)).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(sources.menu).toHaveCount(0);
    await expect(button).toBeFocused();
    await expect(button).toHaveAttribute('aria-expanded', 'false');

    // From the keyboard: Arrow Down on the button opens it too.
    await page.keyboard.press('ArrowDown');
    await expect(sources.menuItems.nth(0)).toBeFocused();

    // Focus leaving the menu closes it.
    await page.keyboard.press('Tab');
    await expect(sources.menu).toHaveCount(0);
  });

  test('keeps one menu open at a time, and offers no site for a query or an odd address', async ({
    page,
  }) => {
    const sources = new SourcesPage(page);
    await sources.open();

    // The lowest row's menu opens below it, clear of the rows above.
    await sources.row(2).actions.click();

    await expect(sources.menuItems).toHaveText(['Edit source', 'Test fetch now', 'Remove source']);

    await sources.row(0).actions.click();

    await expect(sources.menu).toHaveCount(1);
    await expect(sources.menu).toHaveAccessibleName('Actions for Redeemed Run Club Toronto');
    await expect(sources.row(2).actions).toHaveAttribute('aria-expanded', 'false');

    await page.keyboard.press('Escape');
    await sources.row(1).actions.click();

    await expect(sources.menu).toHaveAccessibleName('Actions for Toronto church fitness');
    await expect(sources.menuItems).toHaveText(['Edit source', 'Test fetch now', 'Remove source']);

    // Its own button closes it again.
    await sources.row(1).actions.click();

    await expect(sources.menu).toHaveCount(0);
    await expect(sources.row(1).actions).toHaveAttribute('aria-expanded', 'false');
  });

  test('opens the site in a new tab, away from Keryx', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    const open = sources.menuItem(/^Open redeemedrun\.ca/);

    await expect(open).toHaveAttribute('href', 'https://redeemedrun.ca/feed');
    await expect(open).toHaveAttribute('target', '_blank');
    await expect(open).toHaveAttribute('rel', 'noopener noreferrer');
  });

  test('tests the source and says how it went, leaving the list as it was', async ({
    page,
    api,
  }) => {
    let tests = 0;
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/test', (request) => {
      tests += 1;
      expect(request.headers()['x-xsrf-token']).toBe('tok_1');
      return {
        json:
          tests === 1
            ? { reachable: true, reason: null, items_found: 12, items_relevant: 3 }
            : { reachable: false, reason: 'not_found', items_found: 0, items_relevant: 0 },
      };
    });
    const sources = new SourcesPage(page);
    await sources.open();
    const button = sources.row(0).actions;

    await button.click();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    await expect(sources.toastTitle).toHaveText('Tested Redeemed Run Club Toronto');
    await expect(sources.toastText).toHaveText('Test fetch found 12 items — 3 look relevant.');
    await expect(sources.menu).toHaveCount(0);
    await expect(sources.row(0).health).toHaveText('Failing: 404 Not Found');

    await sources.toastDismiss.click();
    await button.click();
    await sources.menuItem('Test fetch now').click();

    await expect(sources.toastText).toHaveText(
      'Test fetch found nothing at this address (not found). Its health changes when a run fetches it.',
    );
    await expect(sources.toast).toHaveClass(/toast--warning/);
    expect(lists).toBe(1);
    expect(tests).toBe(2);
  });

  test('says when the source was removed meanwhile, and reads the list again', async ({
    page,
    api,
  }) => {
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/test', () => ({
      status: 404,
      json: { status: 404, code: 'source_not_found', trace_id: 'req_4' },
    }));
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    await sources.menuItem('Test fetch now').click();

    await expect(sources.toastTitle).toHaveText('Source not found');
    await expect(sources.toastText).toHaveText(
      'Redeemed Run Club Toronto was removed meanwhile, so the list is read again.',
    );
    await expect.poll(() => lists).toBe(2);
  });

  test('says the test did not run, and runs it on Try again', async ({ page, api }) => {
    let tests = 0;
    api.on('POST', '/admin/v1/sources/src_aaaaaaaaaaaa/test', () => {
      tests += 1;
      return tests === 1
        ? { status: 503, json: { status: 503, trace_id: 'req_9' } }
        : { json: { reachable: true, reason: null, items_found: 1, items_relevant: 1 } };
    });
    const sources = new SourcesPage(page);
    await sources.open();
    await sources.row(0).actions.click();

    await sources.menuItem('Test fetch now').click();

    await expect(sources.toastTitle).toHaveText('Test fetch didn’t run');
    await expect(sources.toastText).toHaveText(
      'Keryx couldn’t reach its API just now. Reference req_9',
    );

    await sources.toastAction.click();

    await expect(sources.toastTitle).toHaveText('Tested Redeemed Run Club Toronto');
    await expect(sources.toastText).toHaveText('Test fetch found 1 item — 1 looks relevant.');
  });
});
