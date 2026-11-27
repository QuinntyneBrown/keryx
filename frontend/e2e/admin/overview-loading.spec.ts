// Acceptance Test
// Traces to: L2-046 AC1; L2-073 AC1
// Description: While Overview's first answers are slow, skeletons shaped like the final layout
// stand in, the region is busy, and the content takes their place without shifting.

import { expect, test } from '../fixtures/test';
import {
  issuePage,
  LAST_14_RUNS,
  latestPage,
  OVERVIEW_NOW,
  overviewStatus,
  runPage,
} from '../fixtures/overview';
import { OverviewPage } from '../page-objects/admin/overview.page';

test.describe('Overview loading', () => {
  test.beforeEach(async ({ page }) => {
    await page.clock.setFixedTime(OVERVIEW_NOW);
  });

  for (const width of [576, 768, 992, 1280]) {
    test(`shows skeletons while every answer is slow, then the content in place at ${width} px`, async ({
      page,
      api,
    }) => {
      await page.setViewportSize({ width, height: 900 });
      let release: () => void = () => undefined;
      const held = new Promise<void>((resolve) => (release = resolve));
      const after =
        <T>(json: T) =>
        async () => {
          await held;
          return { json };
        };
      api.on('GET', '/admin/v1/overview/status', after(overviewStatus()));
      api.on('GET', '/admin/v1/runs', after(runPage(LAST_14_RUNS)));
      api.on('GET', '/admin/v1/articles', after(latestPage()));
      api.on('GET', '/admin/v1/issues', after(issuePage()));
      const overview = new OverviewPage(page);

      await overview.open();

      await expect(overview.latestSkeletonRows).toHaveCount(7);
      await expect(overview.issueSkeletons).toHaveCount(3);
      await expect(overview.statusSkeletons.first()).toBeVisible();
      await expect(overview.body).toHaveAttribute('aria-busy', 'true');
      await expect(overview.loadingStatus).toHaveText('Loading the overview…');
      await expect(overview.latestHeading).toHaveText('Latest articles');
      await expect(overview.attentionHeading).toHaveText('Needs attention');
      const card = await overview.statusCard.boundingBox();
      const rail = await overview.latest.boundingBox();

      release();

      await expect(overview.headline).toHaveText('Completed with 2 failing sources');
      await expect(overview.latestItems).toHaveCount(7);
      await expect(overview.issueCards).toHaveCount(3);
      await expect(overview.statusSkeletons).toHaveCount(0);
      await expect(overview.body).not.toHaveAttribute('aria-busy');
      await expect(overview.loadingStatus).toHaveCount(0);
      // Nothing shifts: the card and the lists start where their skeletons did.
      expect((await overview.statusCard.boundingBox())?.y).toBe(card?.y);
      expect((await overview.latest.boundingBox())?.y).toBe(rail?.y);
    });
  }

  // A section's slow Try again keeps its own busy button; the page is not loading again.
  test('announces no page loading while one section retries slowly', async ({ page, api }) => {
    await page.clock.install({ time: OVERVIEW_NOW });
    let failing = true;
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    api.on('GET', '/admin/v1/articles', async () => {
      if (failing) return { status: 503, json: { status: 503, trace_id: 'req_1' } };
      await held;
      return { json: latestPage() };
    });
    const overview = new OverviewPage(page);
    await overview.open();
    await expect(overview.latestError).toHaveCount(1);
    failing = false;

    await overview.latestError.locator('.btn').click();
    await page.clock.runFor(1000);

    await expect(overview.latestError.locator('.btn')).toHaveAttribute('aria-busy', 'true');
    await expect(overview.loadingStatus).toHaveCount(0);
    await expect(overview.body).not.toHaveAttribute('aria-busy');
    release();
    await expect(overview.latestItems).toHaveCount(7);
  });
});
