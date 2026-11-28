// Acceptance Test
// Traces to: L2-062 AC4, AC5
// Description: Kind chips narrow Sources to one kind from its first page, mark the chip pressed
// and keep the choice in the URL; a kind with no source says so, with a way back to every type
// and "Add source".

import { expect, test } from '../fixtures/test';
import { SourcesPage } from '../page-objects/admin/sources.page';

const source = (n: number, kind = 'rss') => ({
  id: `src_${String(n).padStart(12, 'a')}`,
  kind,
  address: `https://source${n}.ca/feed`,
  organization_name: `Source ${n}`,
  region: 'toronto',
  region_name: 'Toronto',
  health: 'healthy',
  failure_reason: null,
  consecutive_failures: 0,
  held_count: 0,
  articles_30d: 0,
  last_fetched_at: null,
});

/** A page of the 64 sources, 23 of them RSS and none Eventbrite. */
const answer = (query: URLSearchParams) => {
  const kind = query.get('kind');
  const page = Number(query.get('page'));
  const total = kind === 'rss' ? 23 : kind === 'eventbrite' ? 0 : 64;
  const shown = Math.max(0, Math.min(12, total - (page - 1) * 12));
  return {
    items: Array.from({ length: shown }, (_, i) => source((page - 1) * 12 + i + 1, kind ?? 'rss')),
    total,
    page,
    page_size: 12,
    all_count: 64,
    failing_count: 2,
    needs_look_count: 1,
  };
};

test.describe('Sources by kind', () => {
  let queries: string[];

  test.beforeEach(async ({ api }) => {
    queries = [];
    api.on('GET', '/admin/v1/sources', (request) => {
      const query = new URL(request.url()).searchParams;
      queries.push(query.toString());
      return { json: answer(query) };
    });
  });

  test('narrows the list to one kind from its first page', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open('?page=2');

    await expect(sources.chips).toHaveText([
      'All',
      'RSS',
      'Website',
      'Instagram',
      'Eventbrite',
      'News query',
    ]);
    await expect(sources.chip('All')).toHaveAttribute('aria-pressed', 'true');

    await sources.chip('RSS').click();

    await expect(sources.chip('RSS')).toHaveAttribute('aria-pressed', 'true');
    await expect(sources.chip('All')).toHaveAttribute('aria-pressed', 'false');
    await expect(sources.range).toHaveText('Showing 1–12 of 23 RSS sources, failing first.');
    await expect(sources.description).toHaveText(
      '64 sources across the GTA · 2 failing · 1 need a look',
    );
    expect(sources.search()).toBe('?kind=rss');

    await sources.next.click();

    await expect(sources.range).toHaveText('Showing 13–23 of 23 RSS sources, failing first.');
    expect(sources.search()).toBe('?kind=rss&page=2');

    await sources.chip('All').click();

    await expect(sources.range).toHaveText('Showing 1–12 of 64 sources, failing first.');
    expect(sources.search()).toBe('');
    expect(queries).toEqual([
      'page=2&page_size=12',
      'page=1&page_size=12&kind=rss',
      'page=2&page_size=12&kind=rss',
      'page=1&page_size=12',
    ]);
  });

  test('opens the kind the URL names, and ignores one it does not know', async ({ page }) => {
    const sources = new SourcesPage(page);

    await sources.open('?kind=news_query');

    await expect(sources.chip('News query')).toHaveAttribute('aria-pressed', 'true');

    await sources.open('?kind=fax');

    await expect(sources.chip('All')).toHaveAttribute('aria-pressed', 'true');
    expect(queries).toEqual(['page=1&page_size=12&kind=news_query', 'page=1&page_size=12']);
  });

  test('says when no source is of the kind, with a way back', async ({ page }) => {
    const sources = new SourcesPage(page);
    await sources.open();

    await sources.chip('Eventbrite').click();

    await expect(sources.emptyTitle).toHaveText('No Eventbrite sources');
    await expect(sources.emptyText).toHaveText(
      'None of the 64 sources is an Eventbrite organiser. Show every type, or add one.',
    );
    await expect(sources.emptyAction).toHaveText(['Show all types', 'Add source']);
    await expect(sources.table).toHaveCount(0);
    await expect(sources.chip('Eventbrite')).toHaveAttribute('aria-pressed', 'true');

    await sources.emptyAction.filter({ hasText: 'Show all types' }).click();

    await expect(sources.chip('All')).toHaveAttribute('aria-pressed', 'true');
    await expect(sources.rows).toHaveCount(12);
    expect(sources.search()).toBe('');
  });
});
