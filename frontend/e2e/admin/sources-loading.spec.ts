// Acceptance Test
// Traces to: L2-062 AC1; L2-073 AC1
// Description: When an answer takes over 300 ms, Sources shows 12 skeleton rows shaped
// like the table under a busy page body, then the rows in their place.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { SourcesPage } from '../page-objects/admin/sources.page';

test('shows skeleton rows while the first answer is slow, then the sources', async ({
  page,
  api,
}) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
  let answer: () => void = () => undefined;
  const answered = new Promise<void>((resolve) => (answer = resolve));
  api.on('GET', '/admin/v1/sources', async () => {
    await answered;
    return {
      json: {
        items: [
          {
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
            articles_30d: 3,
            last_fetched_at: null,
          },
        ],
        total: 1,
        page: 1,
        page_size: 12,
        all_count: 1,
        failing_count: 0,
        needs_look_count: 0,
      },
    };
  });
  const sources = new SourcesPage(page);
  await page.goto('/sources');
  await sources.heading.waitFor();

  await expect(sources.body).toHaveAttribute('aria-busy', 'true');
  // After 300 ms with no answer (skeletonDelay, whose unit tests time it).
  await expect(sources.skeletonRows).toHaveCount(12);
  await expect(sources.loadingStatus).toHaveText('Loading sources…');
  await expect(sources.columns).toHaveText([
    'Source',
    'Health',
    'Region',
    'Articles · 30d',
    'Last fetched',
  ]);

  answer();

  await expect(sources.row(0).name).toHaveText('Redeemed Run Club Toronto');
  await expect(sources.skeletonRows).toHaveCount(0);
  await expect(sources.body).not.toHaveAttribute('aria-busy');
  await expect(sources.loadingStatus).toHaveCount(0);
});
