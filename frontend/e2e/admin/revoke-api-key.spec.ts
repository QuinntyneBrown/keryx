// Acceptance Test
// Traces to: L2-069, L2-036
// Description: Staff revoke a key through a dialog that states the consequence; there is no Undo.

import { expect, test, type ApiHandlers } from '../fixtures/test';
import { USAGE } from '../fixtures/api';
import { ApiPage } from '../page-objects/admin/api.page';
import { NewApiKeyDialog } from '../page-objects/admin/new-api-key.dialog';
import { RevokeApiKeyDialog } from '../page-objects/admin/revoke-api-key.dialog';

const PRODUCT = 'Kingdom Strong website widget';

const WIDGET = {
  id: 'cli_widget',
  product_name: PRODUCT,
  key_prefix: 'kx_live_Pz0e',
  scopes: ['articles:read'],
  created_at: '2026-11-20T14:00:00Z',
  revoked_at: null,
  requests_24h: 0,
  errors_24h: 0,
  error_rate: 0,
  last_call_at: null,
};

function answerApiScreen(api: ApiHandlers): void {
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  api.on('GET', '/admin/v1/api-usage', () => ({ json: USAGE }));
  api.on('GET', '/admin/v1/api-clients', () => ({
    json: { items: [WIDGET], total: 1, page: 1, page_size: 50 },
  }));
}

async function openRevoke(page: import('@playwright/test').Page) {
  const screen = new ApiPage(page);
  const dialog = new RevokeApiKeyDialog(page);
  await screen.open();
  await screen.revoke(PRODUCT).click();
  await expect(dialog.root).toBeVisible();
  return { screen, dialog };
}

test.describe('Revoke API key', () => {
  // L2-069 AC1.
  test('opens titled for the product, states the consequence, and starts on Cancel', async ({
    page,
    api,
  }) => {
    answerApiScreen(api);
    let sent = 0;
    api.on('POST', '/admin/v1/api-clients/cli_widget/revoke', () => {
      sent++;
      return { json: { client: WIDGET } };
    });
    const { screen, dialog } = await openRevoke(page);

    await expect(dialog.container).toHaveAttribute('aria-modal', 'true');
    await expect(dialog.container).toHaveAccessibleName(`Revoke the key for ${PRODUCT}?`);
    await expect(dialog.container).toHaveAccessibleDescription(
      'Requests with kx_live_Pz0e… stop working immediately.',
    );
    await expect(dialog.panel).toHaveClass(/dialog__panel--sm/);
    await expect(dialog.body).toHaveText(
      'Each new request with this key will get 401 Unauthorized. This can’t be undone — create a new key to reconnect it.',
    );
    await expect(dialog.cancel).toBeFocused();
    await expect(dialog.confirm).toHaveText('Revoke key');

    await dialog.cancel.click();
    await expect(dialog.container).toHaveCount(0);
    await expect(screen.revoke(PRODUCT)).toBeVisible();
    expect(sent).toBe(0);
  });

  // L2-069 AC2, AC3; L2-036 AC1.
  test(
    'a confirmed revoke marks the row and warns, with "New API key" and no Undo',
    { tag: '@critical' },
    async ({ page, api }) => {
      answerApiScreen(api);
      let sent = 0;
      let release!: () => void;
      const answered = new Promise<void>((resolve) => (release = resolve));
      api.on('POST', '/admin/v1/api-clients/cli_widget/revoke', async () => {
        sent++;
        await answered;
        return { json: { client: { ...WIDGET, revoked_at: '2026-11-27T15:00:00Z' } } };
      });
      const { screen, dialog } = await openRevoke(page);

      await dialog.confirm.click();

      // In flight: Close, Cancel, Escape and the backdrop do nothing, and one request is sent.
      await expect(dialog.confirm).toHaveText('Revoking…');
      await page.keyboard.press('Escape');
      await page.mouse.click(5, 5);
      await expect(dialog.cancel).toBeDisabled();
      await expect(dialog.close).toBeDisabled();
      await expect(dialog.root).toBeVisible();
      release();

      await expect(dialog.container).toHaveCount(0);
      expect(sent).toBe(1);
      await expect(screen.badge(PRODUCT)).toHaveText('Revoked');
      await expect(screen.revoke(PRODUCT)).toHaveCount(0);
      await expect(screen.toast).toHaveClass(/toast--warning/);
      await expect(screen.toastTitle).toHaveText('Key revoked');
      await expect(screen.toastText).toHaveText(
        `${PRODUCT} now gets 401 Unauthorized. Create a new key to reconnect it.`,
      );
      await expect(screen.toastAction).toHaveText(['New API key']);
      await expect(screen.newKey).toBeFocused();

      await screen.toastAction.click();
      await expect(new NewApiKeyDialog(page).root).toBeVisible();
    },
  );

  // L2-069 AC4.
  test('a failed revoke says so in the dialog, and the key stays active', async ({ page, api }) => {
    answerApiScreen(api);
    api.on('POST', '/admin/v1/api-clients/cli_widget/revoke', () => ({
      status: 500,
      json: { code: 'internal', trace_id: 'req_0d4f8e63' },
    }));
    const { screen, dialog } = await openRevoke(page);

    await dialog.confirm.click();

    await expect(dialog.failure).toHaveAttribute('role', 'alert');
    await expect(dialog.failure).toContainText('Keryx couldn’t revoke the key');
    await expect(dialog.failure).toContainText('Nothing changed. Try again in a moment.');
    await expect(dialog.failure).toContainText('Reference req_0d4f8e63');
    await expect(dialog.confirm).toHaveText('Revoke key');
    await dialog.cancel.click();
    await expect(screen.revoke(PRODUCT)).toBeVisible();
    await expect(screen.badge(PRODUCT)).toHaveCount(0);
  });
});
