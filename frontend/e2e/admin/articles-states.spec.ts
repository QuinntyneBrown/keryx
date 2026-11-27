// Acceptance Test
// Traces to: L2-055
// Description: Articles shows skeleton rows while slow, an empty state, and why it didn't load

import { expect, test } from '../fixtures/test';
import { articlePage, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

test.describe('Articles loading, empty and error', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
  });

  test('shows skeleton rows while the first answer is slow, then the rows in their place', async ({
    page,
    api,
  }) => {
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (answer = resolve));
    api.on('GET', '/admin/v1/articles', async () => {
      await answered;
      return { json: articlePage(1, 18432) };
    });
    const articles = new ArticlesPage(page);
    await page.goto('/articles');

    await expect(articles.skeletonRows).toHaveCount(10);
    await expect(articles.caption).toHaveText('Articles, newest first');
    await expect(articles.panel).toHaveAttribute('aria-busy', 'true');
    await expect(articles.loadingStatus).toHaveText('Loading articles…');
    await expect(articles.tabs).toHaveText(['All …', 'Needs review …', 'Published …', 'Hidden …']);
    await expect(articles.search).toBeDisabled();
    await expect(articles.region).toBeDisabled();
    await expect(articles.category).toBeDisabled();
    const before = await articles.table.boundingBox();

    answer();

    await expect(articles.row(0).title).toHaveText('Article 1');
    await expect(articles.skeletonRows).toHaveCount(0);
    await expect(articles.panel).not.toHaveAttribute('aria-busy');
    await expect(articles.loadingStatus).toHaveCount(0);
    await expect(articles.search).toBeEnabled();
    const after = await articles.table.boundingBox();
    expect(after!.y).toBe(before!.y);
  });

  test('says no article has been collected yet, with no tabs, table or pager', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/articles/counts', () => ({
      json: { all: 0, needs_review: 0, published: 0, hidden: 0 },
    }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 0) }));
    api.on('GET', '/admin/v1/sources', () => ({
      json: {
        items: [],
        total: 0,
        page: 1,
        page_size: 12,
        all_count: 0,
        failing_count: 0,
        needs_look_count: 0,
      },
    }));
    const articles = new ArticlesPage(page);

    await articles.open();

    await expect(articles.stateTitle).toHaveText('No articles yet');
    await expect(articles.stateText).toHaveText(
      'Articles appear here after a collection finds something.',
    );
    await expect(articles.stateActions).toHaveText(['Check sources']);
    await expect(articles.tabList).toHaveCount(0);
    await expect(articles.table).toHaveCount(0);
    await expect(articles.pagination).toHaveCount(0);

    await articles.stateActions.click();

    await expect(page).toHaveURL(/\/sources$/);
  });

  test('says the list did not load, with its reference, and Try again loads it', async ({
    page,
    api,
  }) => {
    let fail = true;
    api.on('GET', '/admin/v1/articles', () =>
      fail
        ? { status: 503, json: { status: 503, trace_id: 'req_41b7e09a' } }
        : { json: articlePage(1, 18432) },
    );
    const articles = new ArticlesPage(page);

    await articles.open();

    await expect(articles.stateTitle).toHaveText("Articles didn't load");
    await expect(articles.stateText).toHaveText(
      "Keryx couldn't reach its API just now. Collection keeps running on schedule; only this screen is affected. Try again, and if it keeps failing, send the reference below to whoever runs Keryx.",
    );
    await expect(articles.stateReference).toHaveText('Reference req_41b7e09a');
    await expect(articles.stateActions).toHaveText(['Try again', 'Go to overview']);
    await expect(articles.stateActions.nth(1)).toHaveAttribute('href', '/');
    await expect(articles.table).toHaveCount(0);

    fail = false;
    await articles.stateActions.nth(0).click();

    await expect(articles.rows).toHaveCount(10);
    await expect(articles.stateTitle).toHaveCount(0);
  });

  test('keeps the rows shown when the next page fails, and offers Try again', async ({
    page,
    api,
  }) => {
    let fail = false;
    api.on('GET', '/admin/v1/articles', (request) => {
      if (fail) return { status: 503, json: { status: 503, trace_id: 'req_2' } };
      const pageNumber = Number(new URL(request.url()).searchParams.get('page'));
      return { json: articlePage(pageNumber, 18432) };
    });
    const articles = new ArticlesPage(page);
    await articles.open();
    await expect(articles.row(0).title).toHaveText('Article 1');

    fail = true;
    await articles.next.click();

    await expect(articles.toast).toContainText("Articles didn't load");
    await expect(articles.row(0).title).toHaveText('Article 1');
    await expect(articles.range).toHaveText('Showing 1–10 of 18,432 articles');
    await expect(articles.toastAction).toHaveText('Try again');

    fail = false;
    await articles.toastAction.click();

    await expect(articles.row(0).title).toHaveText('Article 11');
  });
});
