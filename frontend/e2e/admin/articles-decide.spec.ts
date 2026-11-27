// Acceptance Test
// Traces to: L2-052 AC1, AC2, AC5
// Description: Staff publish a held article from its panel: one request, the badge, row and tab
// counts follow, and a notification reports it with Undo, which returns it to Needs review.

import { expect, test } from '../fixtures/test';
import { articleDetail, articlePage, articleRow, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

const ID = 'art_000000000002';
const TITLE = 'Toronto youth pastors challenge congregations to 100 km in March';

test.describe('Publish an article', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  });

  for (const [status, actions] of [
    ['needs_review', ['Publish', 'Hide from API']],
    ['published', ['Hide from API']],
    ['hidden', ['Publish']],
  ] as const) {
    test(`offers ${actions.join(' and ')} for a ${status} article`, async ({ page, api }) => {
      api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
      api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
      api.on('GET', `/admin/v1/articles/${ID}`, () => ({ json: articleDetail(2, { status }) }));
      const articles = new ArticlesPage(page);

      await page.goto(`/articles?article=${ID}`);

      await expect(articles.panelActions).toHaveText([...actions]);
    });
  }

  test('publishes a held article once, then shows it published with Undo', async ({
    page,
    api,
  }) => {
    let published = false;
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (answer = resolve));
    const sent: { token: string | null }[] = [];
    const held = { title: TITLE, status: 'needs_review', status_reason: 'below_publish_threshold' };
    api.on('GET', '/admin/v1/articles/counts', () => ({
      json: published
        ? { ...COUNTS, needs_review: COUNTS.needs_review - 1, published: COUNTS.published + 1 }
        : COUNTS,
    }));
    api.on('GET', '/admin/v1/articles', () => ({
      json: {
        ...articlePage(1, 3),
        items: [articleRow(1), { ...articleRow(2), ...held }, articleRow(3)],
      },
    }));
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({ json: articleDetail(2, held) }));
    api.on('POST', `/admin/v1/articles/${ID}/publish`, async (request) => {
      sent.push({ token: request.headers()['x-xsrf-token'] ?? null });
      await answered;
      published = true;
      return {
        json: { id: ID, status: 'published', decided_by: 'staff', previous_status: 'needs_review' },
      };
    });
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);
    await expect(articles.panelStatus).toHaveText('Needs review');

    await articles.publish.click();

    await expect(articles.publish).toHaveAttribute('aria-busy', 'true');
    await expect(articles.hide).toBeDisabled();
    answer();

    await expect(articles.panelStatus).toHaveText('Published');
    await expect(articles.panelActions).toHaveText(['Hide from API']);
    await expect(articles.hide).toBeFocused();
    await expect(articles.panelAlert).toHaveCount(0);
    await expect(articles.row(1).status).toHaveText('Published');
    await expect(articles.tabs).toHaveText([
      'All 18,432',
      'Needs review 5',
      'Published 17,912',
      'Hidden 515',
    ]);
    await expect(articles.toast).toHaveClass(/toast--success/);
    await expect(articles.toast.locator('.toast__title')).toHaveText('Article published');
    await expect(articles.toast.locator('.toast__text')).toHaveText(
      `“${TITLE}” is now served by the API.`,
    );
    await expect(articles.toastAction).toHaveText('Undo');
    expect(sent).toEqual([{ token: 'tok_1' }]);
  });

  test('Undo returns a published article to Needs review and the counts to before', async ({
    page,
    api,
  }) => {
    let status = 'needs_review';
    const reverts: unknown[] = [];
    const held = { title: TITLE, status_reason: 'below_publish_threshold' };
    api.on('GET', '/admin/v1/articles/counts', () => ({
      json:
        status === 'published'
          ? { ...COUNTS, needs_review: COUNTS.needs_review - 1, published: COUNTS.published + 1 }
          : COUNTS,
    }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, {
        ...held,
        status,
        status_reason: status === 'published' ? null : held.status_reason,
        decided_by: status === 'published' ? 'staff' : 'system',
      }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/publish`, () => {
      status = 'published';
      return {
        json: { id: ID, status, decided_by: 'staff', previous_status: 'needs_review' },
      };
    });
    api.on('POST', `/admin/v1/articles/${ID}/revert`, (request) => {
      reverts.push(request.postDataJSON());
      status = 'needs_review';
      return { json: { id: ID, status, decided_by: 'system', previous_status: null } };
    });
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);
    await articles.publish.click();
    await expect(articles.toastAction).toHaveText('Undo');

    await articles.toastAction.click();

    await expect(articles.panelStatus).toHaveText('Needs review');
    await expect(articles.panelActions).toHaveText(['Publish', 'Hide from API']);
    await expect(articles.panelAlert).toHaveText(
      'Held because its relevance score is below the 0.60 publish threshold.',
    );
    await expect(articles.row(1).status).toHaveText('Needs review');
    await expect(articles.tabs).toHaveText([
      'All 18,432',
      'Needs review 6',
      'Published 17,911',
      'Hidden 515',
    ]);
    expect(reverts).toEqual([{ previous_status: 'needs_review' }]);
  });

  test('sends one request when Publish is pressed twice at once', async ({ page, api }) => {
    let sent = 0;
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { status: 'needs_review' }),
    }));
    api.on('POST', `/admin/v1/articles/${ID}/publish`, () => {
      sent += 1;
      return {
        json: { id: ID, status: 'published', decided_by: 'staff', previous_status: 'needs_review' },
      };
    });
    const articles = new ArticlesPage(page);
    await page.goto(`/articles?article=${ID}`);

    await articles.doubleActivate(articles.publish);

    await expect(articles.panelStatus).toHaveText('Published');
    await expect(articles.toast).toHaveCount(1);
    expect(sent).toBe(1);
  });

  test.describe('below 1024 px', () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test('publishes from the dialog, which shows the busy button and then the new status', async ({
      page,
      api,
    }) => {
      let answer: () => void = () => undefined;
      const answered = new Promise<void>((resolve) => (answer = resolve));
      api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
      api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
      api.on('GET', `/admin/v1/articles/${ID}`, () => ({
        json: articleDetail(2, { status: 'hidden', status_reason: 'below_review_floor' }),
      }));
      api.on('POST', `/admin/v1/articles/${ID}/publish`, async () => {
        await answered;
        return {
          json: { id: ID, status: 'published', decided_by: 'staff', previous_status: 'hidden' },
        };
      });
      const articles = new ArticlesPage(page);
      await page.goto(`/articles?article=${ID}`);
      await expect(articles.dialog).toBeVisible();

      await articles.publish.click();

      await expect(articles.publish).toHaveAttribute('aria-busy', 'true');
      answer();
      await expect(articles.panelStatus).toHaveText('Published');
      await expect(articles.panelActions).toHaveText(['Hide from API']);
      await expect(articles.hide).toBeFocused();
    });
  });
});
