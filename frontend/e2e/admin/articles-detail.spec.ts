// Acceptance Test
// Traces to: L2-051, L2-053
// Description: Staff open an article beside the list and read its provenance and relevance

import { expect, test } from '../fixtures/test';
import { articleDetail, articlePage, articleRow, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

const HELD = 'art_000000000002';

test.use({ viewport: { width: 1280, height: 900 }, timezoneId: 'America/Toronto' });

test.describe('Article detail panel', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({
      json: articlePage(1, 3, [
        articleRow(1),
        articleRow(2, {
          title: 'Toronto youth pastors challenge congregations to 100 km in March',
          status: 'needs_review',
        }),
        articleRow(3),
      ]),
    }));
    api.on('GET', `/admin/v1/articles/${HELD}`, () => ({
      json: articleDetail(2, {
        title: 'Toronto youth pastors challenge congregations to 100 km in March',
        organization: 'Ontario Christian Coaches Network',
        category: 'races-challenges',
        region: 'toronto',
        relevance: 0.58,
        status: 'needs_review',
        status_reason: 'below_publish_threshold',
        threshold: 0.6,
        published_at: '2026-11-27T09:00:00Z',
        collected_at: '2026-11-27T11:03:00Z',
      }),
    }));
  });

  test('opens an article beside the list, named in the URL, and closes back to its row', async ({
    page,
  }) => {
    const articles = new ArticlesPage(page);
    await articles.open();

    await articles.row(1).title.click();

    await expect(articles.detail).toBeVisible();
    expect(articles.query().get('article')).toBe(HELD);
    await expect(articles.detail).toHaveAccessibleName(
      'Toronto youth pastors challenge congregations to 100 km in March',
    );
    await expect(articles.panelId).toHaveText(HELD);
    await expect(articles.panelStatus).toHaveText('Needs review');
    await expect(articles.panelTitle).toHaveAttribute('dir', 'auto');
    await expect(articles.panelTitle).toBeFocused();
    await expect(articles.panelSummary).toHaveText(
      'A city-wide step challenge across 40 churches raises funds for winter shelters.',
    );
    await expect(articles.panelLink).toHaveText('Open original (opens in a new tab)');
    await expect(articles.panelLink).toHaveAttribute(
      'href',
      'https://news.example.com/youth-100km',
    );
    await expect(articles.panelLink).toHaveAttribute('target', '_blank');
    await expect(articles.panelLink).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(articles.panelTerms).toHaveText([
      'Organisation',
      'Source',
      'Category',
      'Region',
      'Published',
      'Collected',
      'Relevance',
      'Tags',
    ]);
    await expect(articles.panelValues).toHaveText([
      'Ontario Christian Coaches Network',
      'Google News',
      'Races & Challenges',
      'Toronto',
      '6h ago',
      'Today, 6:03 AM',
      '0.58',
      'challengeyouth',
    ]);
    await expect(articles.panelTags).toHaveText(['challenge', 'youth']);
    await expect(articles.panelAlert).toHaveText(
      'Held because its relevance score is below the 0.60 publish threshold.',
    );
    // From 1024 px the panel sits beside the table, which stays usable.
    await expect(articles.table).toBeVisible();
    const table = await articles.table.boundingBox();
    const panel = await articles.detail.boundingBox();
    expect(panel!.x).toBeGreaterThanOrEqual(table!.x + table!.width);

    await articles.panelClose.click();

    await expect(articles.detail).toHaveCount(0);
    expect(articles.query().has('article')).toBe(false);
    await expect(articles.row(1).title).toBeFocused();
    await expect(articles.panelClose).toHaveCount(0);
  });

  test('opens from Enter on a title and closes with Escape', async ({ page }) => {
    const articles = new ArticlesPage(page);
    await articles.open();
    await expect(articles.panelClose).toHaveCount(0);
    await articles.row(1).title.focus();

    await page.keyboard.press('Enter');

    await expect(articles.panelTitle).toBeFocused();
    await expect(articles.panelClose).toHaveAccessibleName('Close article details');

    await page.keyboard.press('Escape');

    await expect(articles.detail).toHaveCount(0);
    await expect(articles.row(1).title).toBeFocused();
  });

  test('shows the list with the panel open from a shared link', async ({ page }) => {
    const articles = new ArticlesPage(page);

    await articles.open(`?article=${HELD}`);

    await expect(articles.rows).toHaveCount(3);
    await expect(articles.panelId).toHaveText(HELD);
    await expect(articles.panelTitle).toHaveText(
      'Toronto youth pastors challenge congregations to 100 km in March',
    );
  });

  test('shows the list and says so when the linked article does not exist', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/articles/art_zzzzzzzzzzzz', () => ({
      status: 404,
      json: { status: 404, code: 'article_not_found', trace_id: 'req_1' },
    }));
    const articles = new ArticlesPage(page);

    await articles.open('?article=art_zzzzzzzzzzzz');

    await expect(articles.toast).toContainText('Article not found');
    await expect(articles.rows).toHaveCount(3);
    await expect(articles.detail).toHaveCount(0);
    expect(articles.query().has('article')).toBe(false);
  });

  test('shows the list and says so when the article cannot be read', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles/art_000000000001', () => ({
      status: 503,
      json: { status: 503, trace_id: 'req_2' },
    }));
    const articles = new ArticlesPage(page);

    await articles.open('?article=art_000000000001');

    await expect(articles.toast).toContainText('Article didn’t load');
    await expect(articles.rows).toHaveCount(3);
    await expect(articles.detail).toHaveCount(0);
    expect(articles.query().has('article')).toBe(false);
  });

  test('says why a hidden article is hidden, and nothing for a published one', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/articles/art_000000000001', () => ({
      json: articleDetail(1, {
        status: 'hidden',
        status_reason: 'below_review_floor',
        relevance: 0.21,
        published_at: null,
        collected_at: '2026-11-26T23:01:00Z',
        // Staff renamed the source since: the panel names it as it is now.
        source: { id: 'src_aaaaaaaaaaaa', name: 'Grace Run Club' },
        provenance: [
          {
            source_id: 'src_aaaaaaaaaaaa',
            source_name: 'Grace Running Club',
            url: 'https://graceruns.ca/feed',
            first_seen_at: '2026-11-26T23:01:00Z',
            last_seen_at: '2026-11-26T23:01:00Z',
          },
        ],
      }),
    }));
    api.on('GET', '/admin/v1/articles/art_000000000003', () => ({
      json: articleDetail(3, { collected_at: '2026-03-09T22:02:00Z' }),
    }));
    const articles = new ArticlesPage(page);
    await articles.open('?article=art_000000000001');

    await expect(articles.panelStatus).toHaveText('Hidden');
    await expect(articles.panelAlert).toHaveText(
      'Hidden automatically: its relevance score is below 0.45. The API does not serve it.',
    );
    await expect(articles.panelValues.nth(1)).toHaveText('Grace Run Club');
    await expect(articles.panelValues.nth(4)).toHaveText('Unknown');
    await expect(articles.panelValues.nth(5)).toHaveText('Yesterday, 6:01 PM');

    await articles.row(2).title.click();

    await expect(articles.panelId).toHaveText('art_000000000003');
    await expect(articles.panelStatus).toHaveText('Published');
    await expect(articles.panelAlert).toHaveCount(0);
    await expect(articles.panelValues.nth(5)).toHaveText('Mon, Mar 9, 6:02 PM');
  });

  test('renders hostile text as text, no link for a non-web URL, and every tag', async ({
    page,
    api,
  }) => {
    const hostile = '<img src=x onerror=alert(1)>';
    const tags = Array.from({ length: 30 }, (_, i) => `tag ${i + 1}`);
    api.on('GET', '/admin/v1/articles/art_000000000001', () => ({
      json: articleDetail(1, {
        title: hostile,
        summary: '<b>bold</b>',
        organization: '<script>x</script>',
        url: 'javascript:alert(1)',
        tags,
        // Its source was removed: the provenance keeps the name it had.
        source: null,
        provenance: [
          {
            source_id: null,
            source_name: 'Old parish feed',
            url: 'https://example.org/feed',
            first_seen_at: '2026-11-27T11:03:00Z',
            last_seen_at: '2026-11-27T11:03:00Z',
          },
        ],
      }),
    }));
    const articles = new ArticlesPage(page);

    await articles.open('?article=art_000000000001');

    await expect(articles.panelTitle).toHaveText(hostile);
    await expect(articles.panelSummary).toHaveText('<b>bold</b>');
    await expect(articles.panelValues.first()).toHaveText('<script>x</script>');
    await expect(articles.panelValues.nth(1)).toHaveText('Old parish feed');
    await expect(articles.panelLink).toHaveText('Original link unavailable');
    await expect(articles.detail.locator('a.detail-panel__link')).toHaveCount(0);
    await expect(articles.panelTags).toHaveText(tags);
    await expect(page.locator('main img, main b, main script')).toHaveCount(0);
  });
});
