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

const EMPTY = {
  items: [],
  total: 0,
  page: 1,
  page_size: 12,
  all_count: 0,
  failing_count: 0,
  needs_look_count: 0,
};

const ADDED = {
  id: 'src_k3m9p2q7x4ab',
  kind: 'website',
  address: 'https://lifepointfit.ca/events',
  organization_name: 'LifePoint Fitness Ministry',
  region: 'scarborough',
  region_name: 'Scarborough',
  health: 'healthy',
  failure_reason: null,
  consecutive_failures: 0,
  held_count: 0,
  articles_30d: 0,
  last_fetched_at: null,
};

const listOf = (item: unknown) => ({ ...EMPTY, items: [item], total: 1, all_count: 1 });

async function fill(dialog: AddSourceDialog): Promise<void> {
  await dialog.type('Website').check();
  await dialog.url.fill('https://lifepointfit.ca/events');
  await dialog.organisation.fill('LifePoint Fitness Ministry');
  await dialog.region.selectOption({ label: 'Scarborough' });
}

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

  // L2-064 AC3.
  test('says what is wrong, sends nothing and keeps every entry', async ({ page, api }) => {
    answerSourcesScreen(api);
    let posts = 0;
    api.on('POST', '/admin/v1/sources', () => {
      posts += 1;
      return { status: 500 };
    });
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.addSource.click();
    await dialog.type('Website').check();
    await dialog.url.fill('lifepointfit');
    await dialog.region.selectOption({ label: 'Scarborough' });

    await dialog.submit.click();

    await expect(dialog.urlError).toHaveText(
      'Enter a full URL starting with https://, like https://lifepointfit.ca/events.',
    );
    await expect(dialog.organisationError).toHaveText(
      'Enter the organisation’s name as it publishes it.',
    );
    await expect(dialog.url).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.url).toHaveAccessibleDescription(
      'Enter a full URL starting with https://, like https://lifepointfit.ca/events.',
    );
    await expect(dialog.organisation).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.organisation).toHaveAccessibleDescription(
      'Enter the organisation’s name as it publishes it.',
    );
    await expect(dialog.region).not.toHaveAttribute('aria-invalid');
    await expect(dialog.url).toBeFocused();
    await expect(dialog.url).toHaveValue('lifepointfit');
    await expect(dialog.type('Website')).toBeChecked();
    await expect(dialog.region).toHaveValue('scarborough');
    expect(posts).toBe(0);

    // Once fixed, a field's error goes as it is corrected.
    await dialog.url.fill('https://lifepointfit.ca/events');
    await expect(dialog.urlError).toHaveCount(0);
    await expect(dialog.url).not.toHaveAttribute('aria-invalid');
  });

  test('asks for a region and moves focus to it when it is the first wrong field', async ({
    page,
    api,
  }) => {
    answerSourcesScreen(api);
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.addSource.click();
    await dialog.url.fill('https://lifepointfit.ca/events');
    await dialog.organisation.fill('LifePoint Fitness Ministry');

    await dialog.organisation.press('Enter');

    await expect(dialog.regionError).toHaveText('Choose the region the organisation is in.');
    await expect(dialog.region).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.region).toBeFocused();
  });

  // L2-064 AC4 and AC5.
  test(
    'adds the source once, busy until it is saved, then lists it and says so',
    { tag: '@critical' },
    async ({ page, api }) => {
      let lists = 0;
      api.on('GET', '/admin/v1/sources', () => {
        lists += 1;
        return { json: lists === 1 ? EMPTY : listOf(ADDED) };
      });
      api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
      api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
      const posts: { body: unknown; token?: string }[] = [];
      let answer!: () => void;
      const answered = new Promise<void>((resolve) => (answer = resolve));
      api.on('POST', '/admin/v1/sources', async (request) => {
        posts.push({ body: request.postDataJSON(), token: request.headers()['x-xsrf-token'] });
        await answered;
        return { status: 201, json: ADDED };
      });
      const sources = new SourcesPage(page);
      const dialog = new AddSourceDialog(page);
      await sources.open();
      await sources.addSource.click();
      await dialog.type('Website').check();
      await dialog.url.fill('https://lifepointfit.ca/events');
      await dialog.organisation.fill('LifePoint Fitness Ministry');
      await dialog.region.selectOption({ label: 'Scarborough' });

      await dialog.submit.click();

      await expect(dialog.submit).toHaveText('Adding…');
      await expect(dialog.submit).toHaveAttribute('aria-busy', 'true');
      await expect(dialog.submit).toBeDisabled();
      await expect(dialog.cancel).toBeDisabled();
      await expect(dialog.close).toBeDisabled();
      await expect(dialog.url).toBeDisabled();
      await expect(dialog.type('RSS')).toBeDisabled();
      await page.keyboard.press('Escape');
      await expect(dialog.root).toHaveCount(1);
      await dialog.submit.press('Enter');
      expect(posts).toEqual([
        {
          body: {
            kind: 'website',
            address: 'https://lifepointfit.ca/events',
            organization_name: 'LifePoint Fitness Ministry',
            region: 'scarborough',
          },
          token: 'tok_1',
        },
      ]);

      answer();

      await expect(dialog.root).toHaveCount(0);
      await expect(sources.addSource).toBeFocused();
      await expect(sources.row(0).name).toHaveText('LifePoint Fitness Ministry');
      await expect(sources.toast).toHaveClass(/toast--success/);
      await expect(sources.toastTitle).toHaveText('Source added');
      await expect(sources.toastText).toHaveText(
        'LifePoint Fitness Ministry will be checked on the next run.',
      );
      await expect(sources.toastRegion).toHaveAttribute('role', 'status');
      expect(posts).toHaveLength(1);

      await sources.toastDismiss.click();
      await expect(sources.toast).toHaveCount(0);
    },
  );

  test('gives focus to "Add source" after adding from the empty state', async ({ page, api }) => {
    let lists = 0;
    api.on('GET', '/admin/v1/sources', () => {
      lists += 1;
      return { json: lists === 1 ? EMPTY : listOf(ADDED) };
    });
    api.on('GET', '/admin/v1/regions', () => ({ json: { items: REGIONS } }));
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('POST', '/admin/v1/sources', () => ({ status: 201, json: ADDED }));
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.emptyAction.click();
    await fill(dialog);

    await dialog.submit.click();

    await expect(sources.rows).toHaveCount(1);
    await expect(sources.addSource).toBeFocused();
  });

  // L2-064 AC6.
  test('keeps the dialog and every entry when adding fails, with a reference', async ({
    page,
    api,
  }) => {
    answerSourcesScreen(api);
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('POST', '/admin/v1/sources', () => ({
      status: 500,
      json: { code: 'internal_error', trace_id: 'req_5be2c7a1' },
    }));
    const sources = new SourcesPage(page);
    const dialog = new AddSourceDialog(page);
    await sources.open();
    await sources.addSource.click();
    await fill(dialog);

    await dialog.submit.click();

    await expect(dialog.failure).toHaveAttribute('role', 'alert');
    await expect(dialog.failure).toContainText('Keryx couldn’t add the source');
    await expect(dialog.failure).toContainText(
      'Nothing was saved and your entries are still here.',
    );
    await expect(dialog.failure).toContainText('Reference req_5be2c7a1');
    await expect(dialog.url).toHaveValue('https://lifepointfit.ca/events');
    await expect(dialog.organisation).toHaveValue('LifePoint Fitness Ministry');
    await expect(dialog.region).toHaveValue('scarborough');
    await expect(dialog.submit).toHaveText('Add source');
    await expect(dialog.submit).toBeEnabled();
  });

  // L2-064 AC7, and the address the API finds unsafe (L2-024).
  for (const [answer, message] of [
    [{ status: 409, json: { code: 'source_exists' } }, 'This source is already in the list.'],
    [
      {
        status: 400,
        json: { code: 'unsafe_address', errors: { address: ['The address is not public.'] } },
      },
      'Keryx can’t fetch this address: it isn’t on the public internet.',
    ],
  ] as const) {
    test(`says on the URL field when the API answers ${answer.json.code}`, async ({
      page,
      api,
    }) => {
      answerSourcesScreen(api);
      api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
      api.on('POST', '/admin/v1/sources', () => answer);
      const sources = new SourcesPage(page);
      const dialog = new AddSourceDialog(page);
      await sources.open();
      await sources.addSource.click();
      await fill(dialog);

      await dialog.submit.click();

      await expect(dialog.urlError).toHaveText(message);
      await expect(dialog.url).toHaveAttribute('aria-invalid', 'true');
      await expect(dialog.url).toBeFocused();
      await expect(dialog.failure).toHaveCount(0);
      await expect(dialog.root).toHaveCount(1);
    });
  }

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
