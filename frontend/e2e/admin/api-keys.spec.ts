// Acceptance Test
// Traces to: L2-068, L2-035
// Description: Staff create an API key from the API screen; the full key is shown once to copy.

import { expect, test, type ApiHandlers } from '../fixtures/test';
import { USAGE } from '../fixtures/api';
import { ApiPage } from '../page-objects/admin/api.page';
import { NewApiKeyDialog } from '../page-objects/admin/new-api-key.dialog';
import { SourcesPage } from '../page-objects/admin/sources.page';

const KEY = 'kx_live_R8vN2qLm0aXe5TbW9yKc3HdF';

const client = (
  id: string,
  product: string,
  prefix: string,
  scopes: string[],
  revoked = false,
) => ({
  id,
  product_name: product,
  key_prefix: prefix,
  scopes,
  created_at: '2026-11-20T14:00:00Z',
  revoked_at: revoked ? '2026-11-21T09:00:00Z' : null,
  requests_24h: 0,
  errors_24h: 0,
  error_rate: 0,
  last_call_at: null,
});

const CLIENTS = [
  client('cli_mobile', 'Keryx Mobile (iOS & Android)', 'kx_live_7Hq2', [
    'articles:read',
    'events:read',
  ]),
  client('cli_widget', 'Kingdom Strong website widget', 'kx_live_Pz0e', ['articles:read']),
  client('cli_staging', 'Old staging key', 'kx_test_aa03', ['articles:read'], true),
];

const CREATED = {
  client: client('cli_covenant', 'Covenant Cycling site', 'kx_live_R8vN', ['articles:read']),
  key: KEY,
};

const pageOf = (items: unknown[]) => ({ items, total: items.length, page: 1, page_size: 50 });

/** The screen with three clients; "create" answers what a test gives it. */
function answerApiScreen(api: ApiHandlers, items: unknown[] = CLIENTS): void {
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  api.on('GET', '/admin/v1/api-clients', () => ({ json: pageOf(items) }));
}

async function openDialog(page: import('@playwright/test').Page) {
  const screen = new ApiPage(page);
  const dialog = new NewApiKeyDialog(page);
  await screen.open();
  await screen.newKey.click();
  await expect(dialog.root).toBeVisible();
  return { screen, dialog };
}

test.describe('API keys', () => {
  test('the API screen is reached from the navigation and lists each client', async ({
    page,
    api,
  }) => {
    answerApiScreen(api);
    const screen = new ApiPage(page);
    await page.goto('/sources');
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

    await screen.navLink.click();

    await expect(page).toHaveURL(/\/api$/);
    await expect(page).toHaveTitle('API · Keryx');
    await expect(screen.navLink).toHaveAttribute('aria-current', 'page');
    await expect(screen.heading).toHaveText('API');
    await expect(screen.description).toHaveText(
      'Products read Keryx news through this API. Manage who has access and see how it’s used.',
    );
    await expect(screen.products).toHaveText([
      'Keryx Mobile (iOS & Android)',
      'Kingdom Strong website widget',
      'Old staging key',
    ]);
    await expect(screen.scopes('Keryx Mobile (iOS & Android)')).toHaveText([
      'articles:read',
      'events:read',
    ]);
    await expect(screen.row('Kingdom Strong website widget')).toContainText('kx_live_Pz0e…');
    // A revoked client stays listed, marked, with no Revoke (L2-036 AC3).
    await expect(screen.badge('Old staging key')).toHaveText('Revoked');
    await expect(screen.row('Old staging key').getByRole('button')).toHaveCount(0);
    await expect(screen.revoke('Kingdom Strong website widget')).toBeVisible();
  });

  test('with no client, the screen invites the first key', async ({ page, api }) => {
    answerApiScreen(api, []);
    const screen = new ApiPage(page);

    await screen.open();

    await expect(screen.emptyTitle).toHaveText('No products are connected yet');
    await expect(screen.emptyText).toHaveText(
      'Each product that reads Keryx gets its own key, so you can see its usage and revoke it without touching the others.',
    );
    await screen.emptyAction.click();
    await expect(new NewApiKeyDialog(page).root).toBeVisible();
  });

  test('when the clients do not load, the screen says so with a reference and tries again', async ({
    page,
    api,
  }) => {
    let fail = true;
    api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
    api.on('GET', '/admin/v1/api-clients', () =>
      fail
        ? { status: 500, json: { code: 'internal', trace_id: 'req_e7740b2f' } }
        : { json: pageOf(CLIENTS) },
    );
    const screen = new ApiPage(page);

    await screen.open();

    await expect(screen.emptyTitle).toHaveText('API keys and usage didn’t load');
    await expect(screen.emptyReference).toHaveText('Reference req_e7740b2f');
    fail = false;
    await screen.tryAgain.click();
    await expect(screen.products).toHaveCount(3);
  });

  // L2-068 AC1.
  test(
    '"New API key" opens with the name, the scopes, the note, and focus in the name',
    { tag: '@critical' },
    async ({ page, api }) => {
      answerApiScreen(api);
      const { dialog } = await openDialog(page);

      await expect(dialog.container).toHaveAttribute('aria-modal', 'true');
      await expect(dialog.container).toHaveAccessibleName('New API key');
      await expect(dialog.container).toHaveAccessibleDescription(
        'One key per product, so each can be measured and revoked on its own.',
      );
      await expect(dialog.nameLabel).toHaveText('Product name');
      await expect(dialog.nameHelp).toHaveText(
        'The website, app or newsletter that will call Keryx with this key.',
      );
      await expect(dialog.name).toHaveAccessibleDescription(
        'The website, app or newsletter that will call Keryx with this key.',
      );
      await expect(dialog.scopeLegend).toHaveText('Scopes');
      await expect(dialog.scopeTexts).toHaveText([
        'articles:readPublished articles and their summaries',
        'events:readEvents with dates and places',
        'sources:readThe list of organisations and sources',
      ]);
      await expect(dialog.scopes).toHaveCount(3);
      for (const scope of await dialog.scopes.all()) await expect(scope).not.toBeChecked();
      await expect(dialog.note).toHaveText(
        'The key is shown once, right after you create it. Keryx stores only its first characters.',
      );
      await expect(dialog.cancel).toHaveText('Cancel');
      await expect(dialog.submit).toHaveText('Create key');
      expect(await dialog.focusedId()).toBe('key-name');

      await dialog.cancel.click();
      await expect(dialog.container).toHaveCount(0);
    },
  );

  // L2-068 AC2.
  test('with no name and no scope, nothing is sent and both say why', async ({ page, api }) => {
    answerApiScreen(api);
    let sent = 0;
    api.on('POST', '/admin/v1/api-clients', () => {
      sent++;
      return { status: 201, json: CREATED };
    });
    const { dialog } = await openDialog(page);
    await dialog.scope('events:read').focus();

    await dialog.submit.click();

    await expect(dialog.nameError).toHaveText(
      'Enter the product’s name so you can recognise its key later.',
    );
    await expect(dialog.name).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.scopeError).toHaveText('Choose at least one scope.');
    await expect(dialog.scopeGroup).toHaveAccessibleDescription('Choose at least one scope.');
    for (const scope of await dialog.scopes.all())
      await expect(scope).toHaveAttribute('aria-invalid', 'true');
    expect(await dialog.focusedId()).toBe('key-name');
    expect(sent).toBe(0);

    // Each message follows its entry from then on.
    await dialog.scope('events:read').check();
    await expect(dialog.scopeError).toHaveCount(0);
    await expect(dialog.nameError).toBeVisible();
  });

  // L2-068 AC3, L2-035 AC1.
  test(
    'a created key is shown once to copy, and its client is first and new',
    { tag: '@critical' },
    async ({ page, api }) => {
      answerApiScreen(api);
      let body: unknown = null;
      let release!: () => void;
      const answered = new Promise<void>((resolve) => (release = resolve));
      api.on('POST', '/admin/v1/api-clients', async (request) => {
        body = request.postDataJSON();
        await answered;
        return { status: 201, json: CREATED };
      });
      const { screen, dialog } = await openDialog(page);
      await dialog.name.fill('  Covenant Cycling site ');
      await dialog.scope('articles:read').check();

      await dialog.submit.click();

      // Busy: one request, and nothing closes the dialog (design: NewApiKeyDialog).
      await expect(dialog.submit).toHaveText('Creating…');
      await page.keyboard.press('Escape');
      await expect(dialog.root).toBeVisible();
      release();

      await expect(dialog.container).toHaveCount(0);
      expect(body).toEqual({ product_name: 'Covenant Cycling site', scopes: ['articles:read'] });
      await expect(screen.callout).toHaveAttribute('role', 'status');
      await expect(screen.calloutTitle).toHaveText(
        'Key created for “Covenant Cycling site”. Copy it now — it won’t be shown again.',
      );
      await expect(screen.key).toHaveText(KEY);
      await expect(screen.copy).toHaveText('Copy');
      await expect(screen.products.first()).toHaveText('Covenant Cycling site');
      await expect(screen.badge('Covenant Cycling site')).toHaveText('New');
      await expect(screen.products).toHaveCount(4);
    },
  );

  // L2-068 AC4.
  test('"Copy" says the key was copied, or that the browser blocked it and the key stays selectable', async ({
    page,
    api,
  }) => {
    answerApiScreen(api);
    api.on('POST', '/admin/v1/api-clients', () => ({ status: 201, json: CREATED }));
    const { screen, dialog } = await openDialog(page);
    await dialog.name.fill('Covenant Cycling site');
    await dialog.scope('articles:read').check();
    await dialog.submit.click();

    await screen.copy.click();
    await expect(screen.toastTitle).toHaveText('Key copied');

    await screen.blockClipboard();
    await screen.copy.click();
    await expect(screen.toastTitle.last()).toHaveText('Keryx couldn’t copy the key');
    await expect(screen.toastText.last()).toHaveText(
      'Your browser blocked the clipboard. Select the key and copy it by hand.',
    );
    await expect(screen.key).toHaveCSS('user-select', 'all');
  });

  // L2-068 AC5.
  test('the key is gone once staff leave the screen or reload, and is never kept outside it', async ({
    page,
    api,
  }) => {
    answerApiScreen(api);
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
    api.on('POST', '/admin/v1/api-clients', () => ({ status: 201, json: CREATED }));
    const { screen, dialog } = await openDialog(page);
    await dialog.name.fill('Covenant Cycling site');
    await dialog.scope('articles:read').check();
    await dialog.submit.click();
    await expect(screen.key).toHaveText(KEY);
    expect(await screen.keptOutsideTheScreen()).not.toContain(KEY);

    const sources = new SourcesPage(page);
    await sources.navLink.click();
    // Sources loads lazily: going back before it is shown would cancel the visit, not end it.
    await expect(sources.heading).toHaveText('Sources');
    await screen.navLink.click();
    await expect(screen.products).toHaveCount(3);
    await expect(screen.callout).toHaveCount(0);
    expect(await screen.markup()).not.toContain(KEY);

    await dialog.container.waitFor({ state: 'detached' });
    await screen.newKey.click();
    await dialog.name.fill('Covenant Cycling site');
    await dialog.scope('articles:read').check();
    await dialog.submit.click();
    await expect(screen.key).toHaveText(KEY);
    await page.reload();
    await expect(screen.products).toHaveCount(3);
    await expect(screen.callout).toHaveCount(0);
    expect(await screen.markup()).not.toContain(KEY);
  });

  // L2-068 AC6.
  test('a failed create keeps the dialog open with the entries and a reference', async ({
    page,
    api,
  }) => {
    answerApiScreen(api);
    api.on('POST', '/admin/v1/api-clients', () => ({
      status: 500,
      json: { code: 'internal', trace_id: 'req_9f3a2c71' },
    }));
    const { dialog } = await openDialog(page);
    await dialog.name.fill('Covenant Cycling site');
    await dialog.scope('events:read').check();

    await dialog.submit.click();

    await expect(dialog.failure).toContainText('Keryx couldn’t create the key');
    await expect(dialog.failure).toContainText('No key was created. Try again in a moment.');
    await expect(dialog.failure).toContainText('Reference req_9f3a2c71');
    await expect(dialog.name).toHaveValue('Covenant Cycling site');
    await expect(dialog.scope('events:read')).toBeChecked();
    await expect(dialog.submit).toHaveText('Create key');
  });

  // L2-068 AC6, L2-035 AC4.
  test('a name an active client uses is refused on the name field', async ({ page, api }) => {
    answerApiScreen(api);
    api.on('POST', '/admin/v1/api-clients', () => ({
      status: 409,
      json: { code: 'client_name_taken', trace_id: 'req_1' },
    }));
    const { dialog } = await openDialog(page);
    await dialog.name.fill('Kingdom Strong website widget');
    await dialog.scope('articles:read').check();

    await dialog.submit.click();

    await expect(dialog.nameError).toHaveText(
      'A key with that name already exists. Revoke it first, or name this product differently.',
    );
    await expect(dialog.name).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.name).toBeFocused();
    await expect(dialog.failure).toHaveCount(0);
  });
});
