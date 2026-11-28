// Acceptance Test
// Traces to: L2-054 AC1, AC5, AC6
// Description: "Export CSV" exports the articles of the current tab, search and filters. It says
// the export started, with the file's name for today's date in Toronto and the row count, the
// browser saves the file the server produced, and the object URL is revoked once the download
// has started. It is disabled while the list loads, in its error and empty states, and while an
// export is in flight; with no results for a search it stays enabled.

import { readFile } from 'node:fs/promises';
import { expect, test } from '../fixtures/test';
import { articlePage, COUNTS, REGIONS } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';

// 10 PM on Nov 27 in Toronto, already Nov 28 in UTC.
const LATE = new Date('2026-11-28T03:00:00Z');
const CSV = '"id","title"\r\n"art_1","Running clinic"\r\n';

test.describe('Export articles', () => {
  test.beforeEach(async ({ page, api }) => {
    await page.clock.setFixedTime(LATE);
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 18432) }));
  });

  test('exports the current tab, search and filters, and saves the file', async ({ page, api }) => {
    const asked: Record<string, string>[] = [];
    api.on('GET', '/admin/v1/articles/export', (request) => {
      asked.push(Object.fromEntries(new URL(request.url()).searchParams));
      return {
        body: Buffer.from(CSV),
        headers: {
          'content-type': 'text/csv; charset=utf-8',
          'content-disposition': 'attachment; filename=articles-2026-11-27.csv',
        },
      };
    });
    const articles = new ArticlesPage(page);
    await articles.watchObjectUrls();
    await articles.open('?status=needs_review&q=run&region=toronto&category=events&page=2');

    const download = page.waitForEvent('download');
    await articles.exportCsv.click();

    await expect(articles.toast).toHaveClass(/toast--info/);
    await expect(articles.toastTitle).toHaveText('Export started');
    await expect(articles.toastText).toHaveText(
      'articles-2026-11-27.csv will download when it’s ready — 18,432 rows.',
    );
    const saved = await download;
    expect(saved.suggestedFilename()).toBe('articles-2026-11-27.csv');
    expect(await readFile(await saved.path(), 'utf8')).toBe(CSV);
    expect(asked).toEqual([
      { status: 'needs_review', q: 'run', region: 'toronto', category: 'events' },
    ]);
    await expect.poll(() => articles.objectUrls()).toEqual({ created: 1, revoked: 1 });
  });

  test('is disabled while an export is in flight, and enabled again once it ends', async ({
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
    await articles.open();
    await expect(articles.exportCsv).toBeEnabled();

    const download = page.waitForEvent('download');
    await articles.exportCsv.click();
    await expect(articles.exportCsv).toBeDisabled();
    answer();
    await download;

    await expect(articles.exportCsv).toBeEnabled();
  });

  test('is disabled while the list loads, then enabled', async ({ page, api }) => {
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => (answer = resolve));
    api.on('GET', '/admin/v1/articles', async () => {
      await answered;
      return { json: articlePage(1, 18432) };
    });
    const articles = new ArticlesPage(page);
    await articles.open();

    await expect(articles.exportCsv).toBeDisabled();
    answer();
    await expect(articles.rows).toHaveCount(10);
    await expect(articles.exportCsv).toBeEnabled();
  });

  test('is disabled when the list did not load', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles', () => ({
      status: 500,
      json: { code: 'internal', trace_id: 'req_5d2a90c1' },
    }));
    const articles = new ArticlesPage(page);
    await articles.open();

    await expect(articles.stateTitle).toHaveText("Articles didn't load");
    await expect(articles.exportCsv).toBeDisabled();
  });

  test('is disabled when nothing has been collected', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles/counts', () => ({
      json: { all: 0, needs_review: 0, published: 0, hidden: 0 },
    }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 0, []) }));
    const articles = new ArticlesPage(page);
    await articles.open();

    await expect(articles.stateTitle).toHaveText('No articles yet');
    await expect(articles.exportCsv).toBeDisabled();
  });

  test('stays enabled when a search finds nothing', async ({ page, api }) => {
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 0, []) }));
    const articles = new ArticlesPage(page);
    await articles.open('?q=pickleball');

    await expect(articles.emptyTitle).toHaveText('No articles match “pickleball”');
    await expect(articles.exportCsv).toBeEnabled();
  });
});
