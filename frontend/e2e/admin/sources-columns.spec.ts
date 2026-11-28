// Acceptance Test
// Traces to: L2-062 AC1, AC3
// Description: Sources shows each source's yield over 30 days and its last fetch, a failing
// source's health names the reason and, after more than one run, how many, and the table ends
// with its range, failing first, and Previous and Next, which page through the catalogue.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { SourcesPage } from '../page-objects/admin/sources.page';

const source = (n: number, fields: Record<string, unknown> = {}) => ({
  id: `src_${String(n).padStart(12, 'a')}`,
  kind: 'rss',
  address: `https://source${n}.ca/feed`,
  organization_name: `Source ${n}`,
  region: 'toronto',
  region_name: 'Toronto',
  health: 'healthy',
  failure_reason: null,
  consecutive_failures: 0,
  held_count: 0,
  articles_30d: n,
  last_fetched_at: '2026-11-28T11:01:00Z',
  ...fields,
});

/** Page `page` of 64 sources, 12 to a page, as the API serves them. */
const pageOf = (page: number, items: unknown[]) => ({
  items,
  total: 64,
  page,
  page_size: 12,
  all_count: 64,
  failing_count: 2,
  needs_look_count: 1,
});

test.describe('Sources columns and paging', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test('shows yield, last fetch and why a source fails', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources', () => ({
      json: pageOf(1, [
        source(1, {
          organization_name: 'Redeemed Run Club Toronto',
          health: 'failing',
          failure_reason: 'not_found',
          consecutive_failures: 1,
          articles_30d: 0,
        }),
        source(2, {
          organization_name: 'Covenant Cycling Collective',
          health: 'failing',
          failure_reason: 'timeout',
          consecutive_failures: 4,
          articles_30d: 1234,
        }),
        source(3, { last_fetched_at: null }),
        source(4, { last_fetched_at: '2026-11-27T22:15:00Z' }),
      ]),
    }));
    const sources = new SourcesPage(page);

    await sources.open();

    await expect(sources.columns).toHaveText([
      'Source',
      'Health',
      'Region',
      'Articles · 30d',
      'Last fetched',
    ]);
    const redeemed = sources.row(0);
    await expect(redeemed.health).toHaveText('Failing: 404 Not Found');
    await expect(redeemed.articles).toHaveText('0');
    await expect(redeemed.lastFetched).toHaveText('Today, 6:01 AM');
    const covenant = sources.row(1);
    await expect(covenant.health).toHaveText('Failing: Timeout · 4 runs');
    await expect(covenant.articles).toHaveText('1,234');
    await expect(sources.row(2).lastFetched).toHaveText('Not yet');
    await expect(sources.row(3).lastFetched).toHaveText('Yesterday, 5:15 PM');
  });

  test('pages through the catalogue, keeping the page in the URL', async ({ page, api }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/sources', (request) => {
      const query = new URL(request.url()).searchParams;
      queries.push(query.toString());
      const n = Number(query.get('page'));
      return {
        json: pageOf(
          n,
          Array.from({ length: n === 6 ? 4 : 12 }, (_, i) => source((n - 1) * 12 + i + 1)),
        ),
      };
    });
    const sources = new SourcesPage(page);

    await sources.open();

    await expect(sources.range).toHaveText('Showing 1–12 of 64 sources, failing first.');
    await expect(sources.previous).toBeDisabled();

    await sources.next.click();

    await expect(sources.row(0).name).toHaveText('Source 13');
    await expect(sources.range).toHaveText('Showing 13–24 of 64 sources, failing first.');
    expect(sources.search()).toBe('?page=2');
    await expect(sources.previous).toBeEnabled();

    await sources.open('?page=6');

    await expect(sources.range).toHaveText('Showing 61–64 of 64 sources, failing first.');
    await expect(sources.next).toBeDisabled();

    await sources.previous.click();

    await expect(sources.row(0).name).toHaveText('Source 49');

    await sources.open('?page=2');
    await sources.previous.click();

    // The first page needs no parameter.
    await expect(sources.row(0).name).toHaveText('Source 1');
    expect(sources.search()).toBe('');
    expect(queries).toEqual([
      'page=1&page_size=12',
      'page=2&page_size=12',
      'page=6&page_size=12',
      'page=5&page_size=12',
      'page=2&page_size=12',
      'page=1&page_size=12',
    ]);
  });

  test('opens the first page for a page the URL cannot mean', async ({ page, api }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/sources', (request) => {
      queries.push(new URL(request.url()).search);
      return { json: pageOf(1, [source(1)]) };
    });
    const sources = new SourcesPage(page);

    for (const query of ['?page=-2', '?page=1.5', '?page=two']) {
      await sources.open(query);
      await expect(sources.row(0).name).toHaveText('Source 1');
    }

    expect(queries).toEqual(Array(3).fill('?page=1&page_size=12'));
  });

  test('reads the same page of the same kind again on Try again', async ({ page, api }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/sources', (request) => {
      queries.push(new URL(request.url()).search);
      return queries.length === 1
        ? { status: 503, json: { status: 503, trace_id: 'req_1' } }
        : { json: pageOf(3, [source(25)]) };
    });
    const sources = new SourcesPage(page);
    await sources.open('?kind=rss&page=3');

    await sources.tryAgain.click();

    await expect(sources.row(0).name).toHaveText('Source 25');
    expect(queries).toEqual(['?page=3&page_size=12&kind=rss', '?page=3&page_size=12&kind=rss']);
  });
});
