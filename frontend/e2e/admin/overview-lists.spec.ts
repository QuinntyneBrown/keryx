// Acceptance Test
// Traces to: L2-045 AC1, AC2, AC4, AC7
// Description: Overview lists the 7 newest articles and the open issues that need attention,
// errors first, side by side from 1024 px.

import { expect, test } from '../fixtures/test';
import { issuePage, latestPage, OPEN_ISSUES, OVERVIEW_NOW } from '../fixtures/overview';
import { COUNTS, REGIONS, articlePage } from '../fixtures/articles';
import { ArticlesPage } from '../page-objects/admin/articles.page';
import { OverviewPage } from '../page-objects/admin/overview.page';

test.describe('Overview lists', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  test(
    'lists the 7 newest articles and the 3 open issues',
    { tag: '@critical' },
    async ({ page, api }) => {
      const asked: string[] = [];
      api.on('GET', '/admin/v1/articles', (request) => {
        asked.push(new URL(request.url()).search);
        return { json: latestPage() };
      });
      api.on('GET', '/admin/v1/issues', (request) => {
        asked.push(new URL(request.url()).search);
        return { json: issuePage() };
      });
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.latestHeading).toHaveText('Latest articles');
      await expect(overview.viewAll).toHaveText('View all 18,432');
      await expect(overview.viewAll).toHaveAttribute('href', '/articles');
      await expect(overview.latestTitles).toHaveText([
        'Shepherd’s 5K returns to Lakefront Promenade this May',
        'Grace Barbell Club opens second location in Leslieville',
        'Men’s Iron Sharpens Iron bootcamp launches at Crossroads Church',
        'Toronto youth pastors challenge congregations to 100 km in March',
        'Temple Fit Markham adds prenatal strength classes',
        'Faithful Fuel publishes Lenten meal-prep guide for athletes',
        'Torch Athletics hosts free sports camp for Brampton newcomers',
      ]);
      await expect(overview.latestMetas.first()).toHaveText(
        'Shepherd’s 5K · Mississauga · Races & Challenges',
      );
      await expect(overview.latestMetas.nth(2)).toHaveText(
        'Crossroads Church Fitness Ministry · Vaughan · Ministry Programs',
      );
      await expect(overview.latestAges).toHaveText([
        '2h ago',
        '3h ago',
        '5h ago',
        '6h ago',
        '8h ago',
        '11h ago',
        '14h ago',
      ]);
      expect(await overview.latestBadges()).toEqual([null, null, null, 'Review', null, null, null]);

      await expect(overview.attentionHeading).toHaveText('Needs attention');
      await expect(overview.openCount).toHaveText('3 open');
      await expect(overview.issueSeverities).toHaveText(['Error:', 'Error:', 'Warning:']);
      await expect(overview.issueTitles).toHaveText([
        'Feed returned 404',
        'Source timed out',
        'Items held for review',
      ]);
      await expect(overview.issueSources).toHaveText([
        'Redeemed Run Club Toronto',
        'Covenant Cycling Collective',
        'Google News · “Toronto church fitness”',
      ]);
      await expect(overview.issueMetas).toHaveText([
        'Today, 6:01 AM',
        '4 runs in a row · since Mon, 12:02 PM',
        'Today, 6:03 AM',
      ]);
      expect(asked.sort()).toEqual(['?page=1&page_size=7', '?state=open&page_size=5']);
    },
  );

  // L2-045 AC1: "View all" opens Articles inside the app, without loading the document again.
  test('opens Articles from View all', async ({ page, api }) => {
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.viewAll).toHaveText('View all 18,432');
    await overview.markDocument();
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/articles/counts', () => ({ json: COUNTS }));
    api.on('GET', '/admin/v1/articles', () => ({ json: articlePage(1, 18432) }));

    await overview.viewAll.click();

    const articles = new ArticlesPage(page);
    await expect(articles.heading).toHaveText('Articles');
    expect(new URL(page.url()).pathname).toBe('/articles');
    expect(await overview.documentWasKept()).toBe(true);
  });

  // L2-045 AC4.
  test('reads all clear when no issue is open', async ({ page, api }) => {
    api.on('GET', '/admin/v1/issues', () => ({ json: issuePage([]) }));
    const overview = new OverviewPage(page);

    await overview.open();

    await expect(overview.allClear.locator('.empty__title')).toHaveText('Nothing needs attention');
    await expect(overview.allClear.locator('.empty__text')).toHaveText(
      'Every source responded in the last run.',
    );
    await expect(overview.issueCards).toHaveCount(0);
    await expect(overview.openCount).toHaveCount(0);
  });

  // L2-045 AC2: the count is every open issue, though at most 5 are shown.
  for (const [total, items, words] of [
    [8, OPEN_ISSUES, '8 open'],
    [1, OPEN_ISSUES.slice(0, 1), '1 open'],
  ] as const) {
    test(`counts ${words}`, async ({ page, api }) => {
      api.on('GET', '/admin/v1/issues', () => ({ json: issuePage(items, total) }));
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.issueCards).toHaveCount(items.length);
      await expect(overview.openCount).toHaveText(words);
    });
  }

  // L2-045 AC7: stacked below 1024 px, side by side from 1024 px.
  for (const [width, beside] of [
    [1023, false],
    [1024, true],
  ] as const) {
    test(`sets the lists ${beside ? 'side by side' : 'one above the other'} at ${width} px`, async ({
      page,
    }) => {
      await page.setViewportSize({ width, height: 900 });
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.issueCards).toHaveCount(3);
      expect(await overview.sideBySide()).toBe(beside);
    });
  }
});
