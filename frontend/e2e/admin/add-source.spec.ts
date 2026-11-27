// Acceptance Test
// Traces to: L2-064, L2-074
// Description: Staff open "Add a source" from Sources, fill it and close it

import { expect, test, type ApiHandlers } from '../fixtures/test';
import { AddSourceDialog } from '../page-objects/admin/add-source.dialog';
import { SourcesPage } from '../page-objects/admin/sources.page';

const REGIONS = [
  { slug: 'toronto', name: 'Toronto', time_zone: 'America/Toronto' },
  { slug: 'scarborough', name: 'Scarborough', time_zone: 'America/Toronto' },
  { slug: 'mississauga', name: 'Mississauga', time_zone: 'America/Toronto' },
  { slug: 'gta', name: 'Greater Toronto Area', time_zone: 'America/Toronto' },
  { slug: 'global', name: 'Global', time_zone: 'Etc/UTC' },
];

function answerSourcesScreen(api: ApiHandlers): void {
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
  api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
}

test.describe('Add a source', () => {
  // L2-064 AC1.
  test(
    'opens titled, with RSS chosen, the fields, and focus in URL',
    { tag: '@critical' },
    async ({ page, api }) => {
      answerSourcesScreen(api);
      const sources = new SourcesPage(page);
      const dialog = new AddSourceDialog(page);
      await sources.open();

      await sources.addSource.click();

      await expect(dialog.container).toHaveAttribute('aria-modal', 'true');
      await expect(dialog.container).toHaveAccessibleName('Add a source');
      await expect(dialog.container).toHaveAccessibleDescription(
        'Keryx will check it on every scheduled run.',
      );
      await expect(dialog.title).toHaveText('Add a source');
      await expect(dialog.typeLegend).toHaveText('Type');
      expect(await dialog.typeLabels()).toEqual(['RSS', 'Website', 'Instagram', 'Eventbrite']);
      await expect(dialog.type('RSS')).toBeChecked();
      await expect(dialog.urlLabel).toHaveText('URL');
      await expect(dialog.url).toBeFocused();
      await expect(dialog.regionOptions).toHaveText([
        'Choose a region',
        'Toronto',
        'Scarborough',
        'Mississauga',
        'Greater Toronto Area',
        'Global',
      ]);
      await expect(dialog.hint).toHaveText(
        'Paste a URL and Keryx runs a test fetch before anything is added.',
      );
      await expect(dialog.cancel).toHaveText('Cancel');
      await expect(dialog.submit).toHaveText('Add source');
      await expect(dialog.close).toHaveAccessibleName('Close');
    },
  );

  test('opens from the empty state too', async ({ page, api }) => {
    answerSourcesScreen(api);
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();

    await sources.emptyAction.click();

    await expect(dialog.title).toHaveText('Add a source');
  });

  for (const how of ['Cancel', 'Close', 'Escape'] as const) {
    test(`closes with ${how} and gives focus back to "Add source"`, async ({ page, api }) => {
      answerSourcesScreen(api);
      const sources = new SourcesPage(page);
      const dialog = new AddSourceDialog(page);
      await sources.open();
      await sources.addSource.click();
      await expect(dialog.url).toBeFocused();

      if (how === 'Cancel') await dialog.cancel.click();
      else if (how === 'Close') await dialog.close.click();
      else await page.keyboard.press('Escape');

      await expect(dialog.root).toHaveCount(0);
      await expect(sources.addSource).toBeFocused();
    });
  }

  test('keeps the type choice by keyboard', async ({ page, api }) => {
    answerSourcesScreen(api);
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.addSource.click();

    await dialog.type('RSS').focus();
    await page.keyboard.press('ArrowRight');

    await expect(dialog.type('Website')).toBeChecked();
    await expect(dialog.type('RSS')).not.toBeChecked();
  });

  // L2-064 AC8, L2-074: a bottom sheet below 640 px.
  test('is a bottom sheet on a narrow screen', async ({ page, api }) => {
    await page.setViewportSize({ width: 375, height: 700 });
    answerSourcesScreen(api);
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.addSource.click();

    // The sheet slides up as it opens: its box is read once it has settled.
    await expect
      .poll(async () => {
        const box = await dialog.panel.boundingBox();
        return box && [Math.round(box.y + box.height), Math.round(box.width)];
      })
      .toEqual([700, 375]);
    await expect(dialog.submit).toBeInViewport();
  });
});
