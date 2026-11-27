// Acceptance Test
// Traces to: L2-052 AC2, AC3, AC4
// Description: Staff hide a published article with Undo; a decision or an Undo that fails changes
// nothing on screen and offers Try again.

import { expect, test } from '../fixtures/test';
import { articleDetail, articlePage, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

const ID = 'art_000000000002';
const TITLE = 'Shepherd’s 5K returns to Lakefront Promenade this May';
const decision = (status: string, previous: string | null, by = 'staff') => ({
  json: { id: ID, status, decided_by: by, previous_status: previous },
});
const failure = { status: 503, json: { status: 503, trace_id: 'req_1' } };

test.describe('Hide an article, and decisions that fail', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
  });

  test('hides a published article and reports it with Undo, which serves it again', async ({
    page,
    api,
  }) => {
    let status = 'published';
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { title: TITLE, status }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/hide`, () => {
      status = 'hidden';
      return decision('hidden', 'published');
    });
    api.on('POST', `/admin/v1/articles/${ID}/revert`, () => {
      status = 'published';
      return decision('published', null, 'system');
    });
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);

    await articles.hide.click();

    await expect(articles.panelStatus).toHaveText('Hidden');
    await expect(articles.panelActions).toHaveText(['Publish']);
    await expect(articles.publish).toBeFocused();
    await expect(articles.toast).toHaveClass(/toast--info/);
    await expect(articles.toast.locator('.toast__title')).toHaveText('Hidden from the API');
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      `“${TITLE}” is no longer served. It stays in Keryx.`,
    );

    await articles.toastAction.click();

    await expect(articles.panelStatus).toHaveText('Published');
    await expect(articles.panelActions).toHaveText(['Hide from API']);
  });

  test('changes nothing when Publish fails, and Try again publishes', async ({ page, api }) => {
    let fail = true;
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { status: 'needs_review', status_reason: 'below_publish_threshold' }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/publish`, () =>
      fail ? failure : decision('published', 'needs_review'),
    );
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);

    await articles.publish.click();

    await expect(articles.toast).toHaveClass(/toast--danger/);
    await expect(articles.toast.locator('.toast__title')).toHaveText(
      'Keryx couldn’t publish the article',
    );
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      'Nothing changed — it is still waiting in Needs review.',
    );
    await expect(articles.toastAction).toHaveText('Try again');
    await expect(articles.panelStatus).toHaveText('Needs review');
    await expect(articles.panelActions).toHaveText(['Publish', 'Hide from API']);
    await expect(articles.publish).toBeEnabled();
    await expect(articles.hide).toBeEnabled();

    fail = false;
    await articles.toastAction.click();

    await expect(articles.panelStatus).toHaveText('Published');
    await expect(articles.toast.locator('.toast__title')).toHaveText(['Article published']);
  });

  test('says a published article is still served when Hide fails', async ({ page, api }) => {
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { status: 'published' }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/hide`, () => failure);
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);

    await articles.hide.click();

    await expect(articles.toast.locator('.toast__title')).toHaveText(
      'Keryx couldn’t hide the article',
    );
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      'Nothing changed — it is still served by the API.',
    );
    await expect(articles.panelStatus).toHaveText('Published');
  });

  test('keeps the decision when Undo fails, and Try again undoes it', async ({ page, api }) => {
    let fail = true;
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { status: 'published' }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/hide`, () => decision('hidden', 'published'));
    api.on('POST', `/admin/v1/articles/${ID}/revert`, () =>
      fail ? failure : decision('published', null, 'system'),
    );
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);
    await articles.hide.click();
    await expect(articles.toastAction).toHaveText('Undo');

    await articles.toastAction.click();

    await expect(articles.toast.locator('.toast__title')).toHaveText(
      'Keryx couldn’t undo the decision',
    );
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      'Nothing changed — it is still hidden from the API.',
    );
    await expect(articles.panelStatus).toHaveText('Hidden');

    fail = false;
    await articles.toastAction.click();

    await expect(articles.panelStatus).toHaveText('Published');
  });

  test('says a decision can no longer be undone when the server refuses the revert', async ({
    page,
    api,
  }) => {
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { status: 'published' }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/hide`, () => decision('hidden', 'published'));
    api.on('POST', `/admin/v1/articles/${ID}/revert`, () => ({
      status: 409,
      json: { status: 409, code: 'revert_unavailable' },
    }));
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);
    await articles.hide.click();

    await articles.toastAction.click();

    await expect(articles.toast.locator('.toast__title')).toHaveText(
      'Keryx couldn’t undo the decision',
    );
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      'This decision can no longer be undone here. Publish or hide the article again instead.',
    );
    await expect(articles.toastAction).toHaveCount(0);
    await expect(articles.panelStatus).toHaveText('Hidden');
  });
});
