// Acceptance Test
// Traces to: L2-054 AC3; L2-075
// Description: When more than 50,000 articles match, the export is refused and a warning asks
// staff to narrow the search or filters; nothing is saved. When it fails otherwise, a danger
// notification says nothing was downloaded and offers "Try again". An export the screen leaves
// behind is cancelled and says nothing.

import { expect, test } from '../fixtures/test';
import { articlePage, COUNTS, NOW, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';
import { OverviewPage } from '../page-objects/admin/overview.page';

const CSV = '"id","title"\r\n"art_1","Running clinic"\r\n';

test.describe('Export articles refused or failed', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(NOW);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 18432) }));
  });

  test('more than 50,000 rows asks staff to narrow the filters, and saves nothing', async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/articles/export', () => ({
      status: 400,
      json: { code: 'export_too_large', trace_id: 'req_0f3c71aa' },
    }));
    const articles = new ArticlesPage(page);
    await articles.watchObjectUrls();
    await articles.open();

    await articles.exportCsv.click();

    await expect(articles.toast).toHaveClass(/toast--warning/);
    await expect(articles.toastTitle).toHaveText('Too many articles to export');
    await expect(articles.toastText).toHaveText(
      'Exports stop at 50,000 rows. Narrow the search or filters, then export again.',
    );
    await expect(articles.toastAction).toHaveCount(0);
    await expect(articles.exportCsv).toBeEnabled();
    expect(await articles.objectUrls()).toEqual({ created: 0, revoked: 0 });
  });

  test('a failed export says nothing was downloaded, and Try again exports again', async ({
    page,
    api,
  }) => {
    let fail = true;
    let asked = 0;
    api.on('GET', '/admin/v1/articles/export', () => {
      asked += 1;
      return fail
        ? { status: 503, json: { code: 'internal', trace_id: 'req_7c1e04a2' } }
        : { body: Buffer.from(CSV), headers: { 'content-type': 'text/csv; charset=utf-8' } };
    });
    const articles = new ArticlesPage(page);
    await articles.open();

    await articles.exportCsv.click();

    await expect(articles.toast).toHaveClass(/toast--danger/);
    await expect(articles.toastTitle).toHaveText('Keryx couldn’t export the articles');
    await expect(articles.toastText).toHaveText('Nothing was downloaded.');
    await expect(articles.toastAction).toHaveText('Try again');

    fail = false;
    const download = page.waitForEvent('download');
    await articles.toastAction.click();

    expect((await download).suggestedFilename()).toBe('articles-2026-11-27.csv');
    expect(asked).toBe(2);
  });

  test('leaving while an export is in flight cancels it and says nothing', async ({
    page,
    api,
  }) => {
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (answer = resolve));
    api.on('GET', '/admin/v1/articles/export', async () => {
      await answered;
      return { body: Buffer.from(CSV), headers: { 'content-type': 'text/csv; charset=utf-8' } };
    });
    const articles = new ArticlesPage(page);
    await articles.watchObjectUrls();
    await articles.open();
    await articles.exportCsv.click();
    await expect(articles.toastTitle).toHaveText('Export started');

    const overview = new OverviewPage(page);
    await overview.navLink.click();
    await expect(overview.heading).toHaveText('Overview');
    answer();

    // The cancelled export creates no file and raises nothing: the fixture fails on a page error.
    await expect(articles.toastTitle).not.toHaveText(/couldn’t export/);
    expect(await articles.objectUrls()).toEqual({ created: 0, revoked: 0 });
  });
});
