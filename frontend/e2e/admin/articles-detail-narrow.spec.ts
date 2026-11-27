// Acceptance Test
// Traces to: L2-051
// Description: Below 1024 px an opened article covers the screen as a modal dialog

import { expect, test } from '../fixtures/test';
import { articleDetail, articlePage, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

const ID = 'art_000000000002';
const TITLE = 'Toronto youth pastors challenge congregations to 100 km in March';

test.use({ viewport: { width: 390, height: 844 }, timezoneId: 'America/Toronto' });

test.describe('Article detail on a narrow screen', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 3) }));
    api.on('GET', `/admin/v1/articles/${ID}`, () => ({
      json: articleDetail(2, { title: TITLE, status: 'needs_review' }),
    }));
  });

  test('covers the screen as a modal, keeps focus in it, and Escape returns to the row', async ({
    page,
  }) => {
    const articles = new ArticlesPage(page);
    await articles.open();

    await articles.row(1).title.click();

    await expect(articles.dialog).toBeVisible();
    await expect(articles.dialog).toHaveAttribute('aria-modal', 'true');
    await expect(articles.dialog).toHaveAccessibleName(TITLE);
    await expect(articles.dialog.locator('.detail-panel__title')).toBeFocused();
    await expect(articles.panelClose).toHaveAccessibleName('Close article details');
    expect(articles.query().get('article')).toBe(ID);
    // Once it has slid in, it covers the screen.
    await expect
      .poll(async () => {
        const box = await articles.detail.boundingBox();
        return box && [box.x, box.y, box.width, box.height].map(Math.round);
      })
      .toEqual([0, 0, 390, 844]);
    // Focus stays in the dialog: Tab past its last control comes back round inside it.
    for (let i = 0; i < 4; i++) await page.keyboard.press('Tab');
    await expect(articles.dialog.locator(':focus')).toHaveCount(1);

    await page.keyboard.press('Escape');

    await expect(articles.dialog).toHaveCount(0);
    await expect.poll(() => articles.query().has('article')).toBe(false);
    await expect(articles.row(1).title).toBeFocused();
  });

  test('closes from its Close button back to the row', async ({ page }) => {
    const articles = new ArticlesPage(page);
    await articles.open();
    await articles.row(1).title.click();
    await expect(articles.dialog).toBeVisible();

    await articles.panelClose.click();

    await expect(articles.dialog).toHaveCount(0);
    await expect(articles.row(1).title).toBeFocused();
    await expect.poll(() => articles.query().has('article')).toBe(false);
  });

  test('opens the dialog from a shared link, and moves beside the list when the screen widens', async ({
    page,
  }) => {
    const articles = new ArticlesPage(page);

    await articles.open(`?article=${ID}`);

    await expect(articles.dialog).toHaveAccessibleName(TITLE);

    await page.setViewportSize({ width: 1280, height: 900 });

    await expect(articles.dialog).toHaveCount(0);
    await expect(page.locator('main aside.detail-panel')).toBeVisible();
    await expect(articles.panelTitle).toHaveText(TITLE);
    expect(articles.query().get('article')).toBe(ID);

    await page.setViewportSize({ width: 390, height: 844 });

    await expect(articles.dialog).toHaveAccessibleName(TITLE);
    await expect(page.locator('main aside.detail-panel')).toHaveCount(0);
  });
});
