// Acceptance Test
// Traces to: L2-050, L2-053
// Description: Staff narrow Articles by a search, a region and a category, kept in the URL

import { expect, test } from '../fixtures/test';
import { articlePage, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

test.describe('Articles search and filters', () => {
  let queries: string[];

  test.beforeEach(async ({ page, api }) => {
    queries = [];
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', (request) => {
      const url = new URL(request.url());
      queries.push(url.search);
      return { json: articlePage(Number(url.searchParams.get('page')), 40) };
    });
  });

  test('searches once typing pauses, sending one request', async ({ page }) => {
    const articles = new ArticlesPage(page);
    await articles.open();
    await expect(articles.search).toHaveAttribute(
      'placeholder',
      'Search titles, organisations, places',
    );
    await expect(articles.search).toHaveAccessibleName('Search articles');

    // Keys come far faster than 300 ms apart, so every pause but the last is cancelled.
    await articles.search.pressSequentially('pickleball');

    await expect.poll(() => queries.at(-1)).toBe('?page=1&page_size=10&q=pickleball');
    expect(queries).toEqual(['?page=1&page_size=10', '?page=1&page_size=10&q=pickleball']);
    expect(articles.query().get('q')).toBe('pickleball');
    await expect(articles.search).toBeFocused();
  });

  test('sends no search of 1 character', async ({ page }) => {
    await page.clock.install({ time: NOW });
    const articles = new ArticlesPage(page);
    await articles.open();

    await articles.search.pressSequentially('p');
    // Past the pause, when a search of 2 characters would have been sent.
    await page.clock.runFor(1000);
    await articles.region.selectOption({ label: 'Toronto' });

    await expect.poll(() => queries.at(-1)).toBe('?page=1&page_size=10&region=toronto');
    expect(queries).toEqual(['?page=1&page_size=10', '?page=1&page_size=10&region=toronto']);
    expect([...articles.query().keys()]).toEqual(['region']);
  });

  test('combines region and category with the tab and the search, back on page 1', async ({
    page,
  }) => {
    const articles = new ArticlesPage(page);
    await articles.open('?status=needs_review&q=run&page=3');
    await expect(articles.range).toHaveText('Showing 21–30 of 40 articles');
    await expect(articles.search).toHaveValue('run');
    await expect(articles.region).toHaveAccessibleName('Region');
    await expect(articles.region.locator('option')).toHaveText([
      'All regions',
      'Toronto',
      'Mississauga',
      'GTA',
      'Global',
    ]);
    await expect(articles.category).toHaveAccessibleName('Category');
    await expect(articles.category.locator('option')).toHaveText([
      'All categories',
      'Events',
      'Races & Challenges',
      'Gyms & Studios',
      'Ministry Programs',
      'Nutrition',
      'Faith & Wellness',
    ]);

    await articles.region.selectOption({ label: 'Toronto' });

    await expect(articles.range).toHaveText('Showing 1–10 of 40 articles');
    expect(queries.at(-1)).toBe('?page=1&page_size=10&status=needs_review&q=run&region=toronto');

    await articles.category.selectOption({ label: 'Gyms & Studios' });

    await expect
      .poll(() => queries.at(-1))
      .toBe('?page=1&page_size=10&status=needs_review&q=run&region=toronto&category=gyms-studios');
    expect(Object.fromEntries(articles.query())).toEqual({
      status: 'needs_review',
      q: 'run',
      region: 'toronto',
      category: 'gyms-studios',
    });

    await articles.region.selectOption({ label: 'All regions' });

    await expect
      .poll(() => queries.at(-1))
      .toBe('?page=1&page_size=10&status=needs_review&q=run&category=gyms-studios');
  });

  test('restores the previous list on Back', async ({ page }) => {
    const articles = new ArticlesPage(page);
    await articles.open();
    await articles.region.selectOption({ label: 'GTA' });
    await expect.poll(() => queries.at(-1)).toBe('?page=1&page_size=10&region=gta');

    await page.goBack();

    await expect.poll(() => queries.at(-1)).toBe('?page=1&page_size=10');
    await expect(articles.region).toHaveValue('');
    expect(articles.query().has('region')).toBe(false);
  });

  test('says nothing matched, and Clear search restores the list and the field', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/articles', (request) => {
      const url = new URL(request.url());
      queries.push(url.search);
      const total = url.searchParams.has('q') ? 0 : 40;
      return { json: articlePage(1, total) };
    });
    const articles = new ArticlesPage(page);
    await articles.open('?q=pickleball&region=toronto');

    await expect(articles.emptyTitle).toHaveText('No articles match “pickleball”');
    await expect(articles.emptyText).toHaveText(
      'Try a different word, or clear the search to see all 18,432 articles.',
    );
    await expect(articles.table).toHaveCount(0);
    await expect(articles.pagination).toHaveCount(0);

    await articles.clearSearch.click();

    await expect(articles.rows).toHaveCount(10);
    await expect(articles.search).toHaveValue('');
    await expect(articles.search).toBeFocused();
    await expect(articles.region).toHaveValue('');
    expect(queries.at(-1)).toBe('?page=1&page_size=10');
    expect([...articles.query().keys()]).toEqual([]);
  });

  test('sends a term with markup and wildcards as literal text and shows it as text', async ({
    page,
    api,
  }) => {
    const term = '<img src=x onerror=alert(1)> 100%_"off"';
    const sent: (string | null)[] = [];
    api.on('GET', '/admin/v1/articles', (request) => {
      sent.push(new URL(request.url()).searchParams.get('q'));
      return { json: articlePage(1, 0) };
    });
    const articles = new ArticlesPage(page);
    await articles.open();

    await articles.search.fill(term);

    await expect(articles.emptyTitle).toHaveText(`No articles match “${term}”`);
    expect(sent.at(-1)).toBe(term);
    await expect(page.locator('main img')).toHaveCount(0);
  });
});
