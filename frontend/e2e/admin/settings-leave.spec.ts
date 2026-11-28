// Acceptance Test
// Traces to: L2-071 AC6
// Description: With unsaved changes, going to another screen asks first: Stay, Escape or Close
// keeps every entry on Settings, and Leave goes. Closing the tab or reloading raises the
// browser's own prompt. With nothing unsaved, nothing asks.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { savedSettings } from '../fixtures/settings';
import { LeaveSettingsDialog } from '../page-objects/admin/leave-settings.dialog';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('asks before leaving with unsaved changes, and Stay loses nothing', async ({ page }) => {
  const settings = new SettingsPage(page);
  const dialog = new LeaveSettingsDialog(page);
  await settings.open();
  await settings.timeout.fill('45');

  await settings.navigation.getByRole('link', { name: 'Overview' }).click();

  await expect(page.getByRole('dialog', { name: 'Leave without saving?' })).toHaveAttribute(
    'aria-modal',
    'true',
  );
  await expect(dialog.description).toHaveText('Your changes to Settings aren’t saved.');
  await expect(dialog.text).toHaveText(
    'If you leave now, they’re lost and Keryx keeps its saved settings. Stay to save or discard them.',
  );
  await expect(dialog.stay).toBeFocused();

  await dialog.stay.click();

  await expect(dialog.root).toHaveCount(0);
  expect(settings.path()).toBe('/settings');
  await expect(settings.timeout).toHaveValue('45');
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');

  await settings.navigation.getByRole('link', { name: 'Overview' }).click();
  await page.keyboard.press('Escape');

  await expect(dialog.root).toHaveCount(0);
  expect(settings.path()).toBe('/settings');
  await expect(settings.timeout).toHaveValue('45');
});

test('leaves on Leave', async ({ page }) => {
  const settings = new SettingsPage(page);
  const dialog = new LeaveSettingsDialog(page);
  await settings.open();
  await settings.switchNamed('National Canadian news').press('Space');

  await settings.navigation.getByRole('link', { name: 'Overview' }).click();
  await dialog.leave.click();

  await expect(page).toHaveURL(/\/$/);
  await expect(dialog.root).toHaveCount(0);
  // Settings is gone, and with it the browser's prompt.
  await expectClosesWithoutPrompt(page);
});

test('leaves without asking when nothing is unsaved', async ({ page }) => {
  const settings = new SettingsPage(page);
  const dialog = new LeaveSettingsDialog(page);
  await settings.open();
  await settings.timeout.fill('45');
  await settings.discard.click();

  await settings.navigation.getByRole('link', { name: 'Overview' }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(dialog.root).toHaveCount(0);
});

test('raises the browser’s prompt on reload only while changes are unsaved', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();
  const prompts: string[] = [];
  page.on('dialog', async (prompt) => {
    prompts.push(prompt.type());
    await prompt.dismiss();
  });
  await settings.timeout.fill('45');

  await page.close({ runBeforeUnload: true });
  await expect.poll(() => prompts).toEqual(['beforeunload']);
  expect(page.isClosed()).toBe(false);

  await settings.discard.click();
  await page.close({ runBeforeUnload: true });
  await expect.poll(() => page.isClosed()).toBe(true);
  expect(prompts).toEqual(['beforeunload']);
});

for (const [what, answer] of [
  ['the save', { json: { ...savedSettings({ timeout_seconds: 45 }), version: '8' } }],
  ['Reload settings after a refused save', { status: 409, json: { code: 'settings_changed' } }],
] as const) {
  test(`raises no prompt once ${what} leaves nothing unsaved`, async ({ page, api }) => {
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    api.on('PUT', '/admin/v1/settings', () => answer);
    const settings = new SettingsPage(page);
    await settings.open();
    await settings.timeout.fill('45');
    await settings.save.click();
    if ('status' in answer) await settings.reload.click();
    else await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);

    await expect(settings.saveBar.filter({ hasText: 'unsaved' })).toHaveCount(0);
    await expectClosesWithoutPrompt(page);
  });
}

/** The page closes at once: no `beforeunload` listener asks first. */
async function expectClosesWithoutPrompt(page: import('@playwright/test').Page): Promise<void> {
  const prompts: string[] = [];
  page.on('dialog', async (prompt) => {
    prompts.push(prompt.type());
    await prompt.dismiss();
  });
  await page.close({ runBeforeUnload: true });
  await expect.poll(() => page.isClosed()).toBe(true);
  expect(prompts).toEqual([]);
}
