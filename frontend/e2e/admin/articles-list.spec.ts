// Acceptance Test
// Traces to: L2-048, L2-049
// Description: Staff page through every article by status, with the server's totals in the tabs

import { expect, test } from '../fixtures/test';
import { articlePage, articleRow, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

test.describe('Articles', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
  });

  test(
    'shows the first page of every article, newest first, under the status totals',
    { tag: '@critical' },
    async ({ page, api }) => {
      const queries: string[] = [];
      const rows = [
        articleRow(1, {
          title: 'Shepherd’s 5K returns to Lakefront Promenade this May',
          organization: 'Shepherd’s 5K',
          category: 'races-challenges',
          region: 'mississauga',
          relevance: 0.94,
          collected_at: '2026-11-27T13:00:00Z',
        }),
        articleRow(2, {
          title: null,
          organization: null,
          category: 'faith-wellness',
          region: 'toronto',
          relevance: 0.58,
          status: 'needs_review',
          collected_at: '2026-11-26T09:00:00Z',
        }),
        articleRow(3, {
          category: 'gyms-studios',
          region: 'global',
          relevance: 0.31,
          status: 'hidden',
          collected_at: '2026-11-27T14:35:00Z',
        }),
        ...Array.from({ length: 7 }, (_, i) => articleRow(4 + i)),
      ];
      api.on('GET', '/admin/v1/articles', (request) => {
        queries.push(new URL(request.url()).search);
        return { json: articlePage(1, 18432, rows) };
      });
      const articles = new ArticlesPage(page);

      await articles.open();

      await expect(articles.heading).toHaveText('Articles');
      await expect(articles.description).toHaveText(
        'Everything Keryx has collected. Only published articles are served by the API.',
      );
      await expect(articles.navLink).toHaveAttribute('aria-current', 'page');
      await expect(articles.tabList).toHaveAccessibleName('Article status');
      await expect(articles.tabs).toHaveText([
        'All 18,432',
        'Needs review 6',
        'Published 17,911',
        'Hidden 515',
      ]);
      await expect(articles.tab('All 18,432')).toHaveAttribute('aria-selected', 'true');
      await expect(articles.panel).toHaveAttribute('aria-labelledby', 'tab-all');
      await expect(articles.caption).toHaveText('Articles, newest first');
      await expect(articles.columns).toHaveText([
        'Article',
        'Category',
        'Region',
        'Relevance',
        'Status',
      ]);
      await expect(articles.rows).toHaveCount(10);
      const first = articles.row(0);
      await expect(first.title).toHaveText('Shepherd’s 5K returns to Lakefront Promenade this May');
      await expect(first.title).toHaveAttribute('dir', 'auto');
      await expect(first.sub).toHaveText('Shepherd’s 5K · 2h ago');
      await expect(first.category).toHaveText('Races & Challenges');
      await expect(first.region).toHaveText('Mississauga');
      await expect(first.relevance).toHaveText('0.94');
      await expect(first.meter).toHaveClass(/meter--success/);
      await expect(first.fill).toHaveAttribute('width', '94');
      await expect(first.status).toHaveText('Published');
      const held = articles.row(1);
      await expect(held.title).toHaveText('Untitled post');
      await expect(held.sub).toHaveText('Unknown · 1d ago');
      await expect(held.category).toHaveText('Faith & Wellness');
      await expect(held.relevance).toHaveText('0.58');
      await expect(held.meter).toHaveClass(/meter--warning/);
      await expect(held.status).toHaveText('Needs review');
      const hidden = articles.row(2);
      await expect(hidden.sub).toHaveText('Redeemed Run Club · 25m ago');
      await expect(hidden.region).toHaveText('Global');
      await expect(hidden.meter).not.toHaveClass(/meter--/);
      await expect(hidden.status).toHaveText('Hidden');
      await expect(articles.range).toHaveText('Showing 1–10 of 18,432 articles');
      await expect(articles.pagination).toHaveAccessibleName('Pagination');
      await expect(articles.previous).toBeDisabled();
      await expect(articles.next).toBeEnabled();
      expect(queries).toEqual(['?page=1&page_size=10']);
    },
  );

  test('loads the next page, keeps it in the URL and moves focus to the caption', async ({
    page,
    api,
  }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/articles', (request) => {
      const url = new URL(request.url());
      queries.push(url.search);
      return { json: articlePage(Number(url.searchParams.get('page')), 18432) };
    });
    const articles = new ArticlesPage(page);
    await articles.open();
    await expect(articles.row(0).title).toHaveText('Article 1');

    await articles.next.click();

    await expect(articles.row(0).title).toHaveText('Article 11');
    await expect(articles.rows).toHaveCount(10);
    await expect(articles.range).toHaveText('Showing 11–20 of 18,432 articles');
    await expect(articles.caption).toBeFocused();
    await expect(articles.previous).toBeEnabled();
    expect(articles.query().get('page')).toBe('2');
    expect(queries).toEqual(['?page=1&page_size=10', '?page=2&page_size=10']);

    await articles.previous.click();

    await expect(articles.range).toHaveText('Showing 1–10 of 18,432 articles');
    expect(articles.query().has('page')).toBe(false);
  });

  test('disables Next on the last page and names a short last page', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles', (request) => {
      const url = new URL(request.url());
      return { json: articlePage(Number(url.searchParams.get('page')), 23) };
    });
    const articles = new ArticlesPage(page);

    await articles.open('?page=3');

    await expect(articles.rows).toHaveCount(3);
    await expect(articles.range).toHaveText('Showing 21–23 of 23 articles');
    await expect(articles.next).toBeDisabled();
    await expect(articles.previous).toBeEnabled();
  });

  test('lists one status from its tab and keeps it in the URL', async ({ page, api }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/articles', (request) => {
      const url = new URL(request.url());
      queries.push(url.search);
      const total = url.searchParams.has('status') ? 6 : 18432;
      return { json: articlePage(Number(url.searchParams.get('page')), total) };
    });
    const articles = new ArticlesPage(page);
    await articles.open('?page=2');
    await expect(articles.range).toHaveText('Showing 11–20 of 18,432 articles');

    await articles.tab('Needs review 6').click();

    await expect(articles.tab('Needs review 6')).toHaveAttribute('aria-selected', 'true');
    await expect(articles.tab('All 18,432')).toHaveAttribute('aria-selected', 'false');
    await expect(articles.panel).toHaveAttribute('aria-labelledby', 'tab-review');
    await expect(articles.range).toHaveText('Showing 1–6 of 6 articles');
    expect(articles.query().toString()).toBe('status=needs_review');
    expect(queries.at(-1)).toBe('?page=1&page_size=10&status=needs_review');

    await page.reload();

    await expect(articles.tab('Needs review 6')).toHaveAttribute('aria-selected', 'true');
    expect(queries.at(-1)).toBe('?page=1&page_size=10&status=needs_review');
  });

  test('moves between tabs with the arrow keys, Home and End', async ({ page, api }) => {
    const statuses: (string | null)[] = [];
    api.on('GET', '/admin/v1/articles', (request) => {
      statuses.push(new URL(request.url()).searchParams.get('status'));
      return { json: articlePage(1, 1) };
    });
    const articles = new ArticlesPage(page);
    await articles.open();
    await expect(articles.rows).toHaveCount(1);
    await expect(articles.tabs.nth(0)).toHaveAttribute('tabindex', '0');
    await expect(articles.tabs.nth(1)).toHaveAttribute('tabindex', '-1');
    await articles.tab('All 18,432').focus();

    // Each key selects the tab it moves to (automatic activation) and lists that status.
    for (const [key, name, status] of [
      ['ArrowRight', 'Needs review 6', 'needs_review'],
      ['End', 'Hidden 515', 'hidden'],
      ['ArrowRight', 'All 18,432', null],
      ['ArrowLeft', 'Hidden 515', 'hidden'],
      ['Home', 'All 18,432', null],
    ] as const) {
      await page.keyboard.press(key);
      await expect(articles.tab(name)).toBeFocused();
      await expect(articles.tab(name)).toHaveAttribute('aria-selected', 'true');
      await expect(articles.tab(name)).toHaveAttribute('tabindex', '0');
      await expect.poll(() => articles.query().get('status')).toBe(status);
      await expect.poll(() => statuses.at(-1)).toBe(status);
    }
    expect(statuses).toEqual([null, 'needs_review', 'hidden', null, 'hidden', null]);
  });

  test('keeps up with keys pressed faster than the list loads', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 1) }));
    const articles = new ArticlesPage(page);
    await articles.open('?status=hidden');
    await expect(articles.rows).toHaveCount(1);
    await articles.tab('Hidden 515').focus();

    // Away to All and back to Hidden before the first move reaches the URL.
    await articles.pressKeysAtOnce(['ArrowRight', 'ArrowLeft']);

    await expect(articles.tab('Hidden 515')).toBeFocused();
    await expect.poll(() => articles.query().get('status')).toBe('hidden');
    await expect(articles.tab('Hidden 515')).toHaveAttribute('aria-selected', 'true');
  });
});
