// Acceptance Test
// Traces to: L2-001, L2-062
// Description: Staff open Sources and see the catalogue, its empty state, or why it didn't load

import { expect, test } from '../fixtures/test';
import { SourcesPage } from '../page-objects/admin/sources.page';

const page1 = (items: unknown[], counts = { all: items.length, failing: 0, needsLook: 0 }) => ({
  items,
  total: items.length,
  page: 1,
  page_size: 12,
  all_count: counts.all,
  failing_count: counts.failing,
  needs_look_count: counts.needsLook,
});

const source = (fields: Record<string, unknown>) => ({
  id: 'src_aaaaaaaaaaaa',
  kind: 'rss',
  address: 'https://redeemedrun.ca/feed',
  organization_name: 'Redeemed Run Club Toronto',
  region: 'toronto',
  region_name: 'Toronto',
  health: 'healthy',
  failure_reason: null,
  consecutive_failures: 0,
  held_count: 0,
  articles_30d: 0,
  last_fetched_at: null,
  ...fields,
});

test.describe('Sources', () => {
  test('shows the first-source state when the catalogue is empty', async ({ page, api }) => {
    const queries: string[] = [];
    api.on('GET', '/admin/v1/sources', (request) => {
      queries.push(new URL(request.url()).search);
      return { json: page1([]) };
    });
    const sources = new SourcesPage(page);

    await sources.open();

    await expect(sources.heading).toHaveText('Sources');
    await expect(sources.description).toHaveText('No sources yet.');
    await expect(sources.emptyTitle).toHaveText('Add the first source');
    await expect(sources.emptyText).toContainText('A source is a feed, page, Instagram account');
    await expect(sources.emptyAction).toHaveText('Add a source');
    await expect(sources.addSource).toHaveText('Add source');
    await expect(sources.navLink).toHaveAttribute('aria-current', 'page');
    await expect(sources.table).toHaveCount(0);
    expect(queries).toEqual(['?page=1&page_size=12']);
  });

  test(
    'lists each source with its name, address, health and region, failing first',
    { tag: '@critical' },
    async ({ page, api }) => {
      api.on('GET', '/admin/v1/sources', () => ({
        json: page1(
          [
            source({
              id: 'src_bbbbbbbbbbbb',
              organization_name: 'Covenant Cycling Collective',
              kind: 'website',
              address: 'https://www.covenantcycling.ca/rides',
              region: 'oakville',
              region_name: 'Oakville',
              health: 'failing',
            }),
            source({
              id: 'src_cccccccccccc',
              organization_name: 'Toronto church fitness',
              kind: 'news_query',
              address: '"church" AND (fitness OR run) near:Toronto',
              region: 'gta',
              region_name: 'Greater Toronto Area',
              health: 'needs_look',
            }),
            source({}),
          ],
          { all: 3, failing: 1, needsLook: 1 },
        ),
      }));
      const sources = new SourcesPage(page);

      await sources.open();

      await expect(sources.description).toHaveText(
        '3 sources across the GTA · 1 failing · 1 need a look',
      );
      await expect(sources.caption).toHaveText('Sources, failing first');
      await expect(sources.columns).toHaveText([
        'Source',
        'Health',
        'Region',
        'Articles · 30d',
        'Last fetched',
        'Actions',
      ]);
      await expect(sources.rows).toHaveCount(3);
      const failing = sources.row(0);
      await expect(failing.name).toHaveText('Covenant Cycling Collective');
      await expect(failing.address).toHaveText('Website: covenantcycling.ca/rides');
      await expect(failing.health).toHaveText('Failing');
      await expect(failing.region).toHaveText('Oakville');
      const query = sources.row(1);
      await expect(query.address).toHaveText(
        'News query: "church" AND (fitness OR run) near:Toronto',
      );
      await expect(query.health).toHaveText('Needs a look');
      await expect(sources.row(2).health).toHaveText('Healthy');
      await expect(sources.row(2).address).toHaveText('RSS: redeemedrun.ca/feed');
    },
  );

  test('renders a name that holds markup as text', async ({ page, api }) => {
    api.on('GET', '/admin/v1/sources', () => ({
      json: page1([source({ organization_name: '<img src=x onerror=alert(1)>Grace' })]),
    }));
    const sources = new SourcesPage(page);

    await sources.open();

    await expect(sources.row(0).name).toHaveText('<img src=x onerror=alert(1)>Grace');
    await expect(sources.table.locator('img')).toHaveCount(0);
  });

  test('says the catalogue did not load, with a reference, and tries again', async ({
    page,
    api,
  }) => {
    let calls = 0;
    api.on('GET', '/admin/v1/sources', () => {
      calls += 1;
      return calls === 1
        ? { status: 503, json: { code: 'internal_error', trace_id: 'req_9a61f3c7' } }
        : { json: page1([source({})]) };
    });
    const sources = new SourcesPage(page);

    await sources.open();

    await expect(sources.emptyTitle).toHaveText('Sources didn’t load');
    await expect(sources.empty).toHaveAttribute('role', 'alert');
    await expect(sources.emptyReference).toHaveText('Reference req_9a61f3c7');
    await expect(sources.description).toHaveText(
      'Feeds, pages and queries Keryx checks on every run.',
    );
    await expect(sources.goToOverview).toHaveAttribute('href', '/');

    await sources.tryAgain.click();

    await expect(sources.rows).toHaveCount(1);
  });
});
