// Acceptance Test
// Traces to: L2-071 AC4, AC5, AC7; L2-070 AC5
// Description: Saving valid changes sends one request with the loaded version while the form is
// busy and cannot be changed; the bar then confirms the save for 8 seconds or until the next
// edit. A failed save keeps the entries and the bar and gives a reference; a save refused because
// the settings changed elsewhere overwrites nothing and offers to reload them.

import type { Request } from '@playwright/test';
import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { savedSettings } from '../fixtures/settings';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page, api }) => {
  // 9:46 AM in Toronto, the mocks' time.
  await page.clock.setFixedTime(OVERVIEW_NOW);
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
});

test('sends one save with the loaded version while nothing can change, then confirms it', async ({
  page,
  api,
}) => {
  const sent: Request[] = [];
  let answer: () => void = () => undefined;
  const answered = new Promise<void>((resolve) => (answer = resolve));
  api.on('PUT', '/admin/v1/settings', async (request) => {
    sent.push(request);
    await answered;
    return { json: { ...request.postDataJSON(), version: '8' } };
  });
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.option('3 hours').click();
  await settings.timeout.fill(' 45 ');
  await settings.alertEmail.fill('');

  await settings.save.click();

  await expect(settings.form).toHaveAttribute('aria-busy', 'true');
  await expect(settings.saveBarText).toHaveText('Saving your changes…');
  await expect(settings.saveBarText).toHaveAttribute('role', 'status');
  await expect(settings.saving).toBeDisabled();
  await expect(settings.saving).toHaveAttribute('aria-busy', 'true');
  await expect(settings.discard).toBeDisabled();
  await expect(settings.timeout).toBeDisabled();
  await expect(settings.option('6 hours')).toBeDisabled();
  await expect(settings.threshold).toBeDisabled();
  await expect(settings.switchNamed('National Canadian news')).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Remove fitness' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Add to Topics' })).toBeDisabled();
  // Every field and chip button of the form, not Account's Sign out.
  const controls = await settings.form.locator('input, .chips button').all();
  // Five entries and five switches; each chip's remove button and each list's Add.
  expect(controls).toHaveLength(5 + 5 + (8 + 1) + (3 + 1) + (9 + 1));
  for (const control of controls) await expect(control).toBeDisabled();
  await settings.saving.click({ force: true });

  answer();

  await expect(settings.saveBar).toHaveAttribute('role', 'status');
  await expect(settings.saveBarText).toHaveText(
    'Saved at 9:46 AM. Keryx now collects every 3 hours — the next run is at 12:00 PM.',
  );
  await expect(settings.saveBarText).toHaveClass(/save-bar__text--success/);
  await expect(settings.viewRuns).toHaveAttribute('href', '/runs');
  await expect(settings.form).not.toHaveAttribute('aria-busy');
  await expect(settings.timeout).toBeEnabled();
  await expect(settings.timeout).toHaveValue('45');
  expect(sent).toHaveLength(1);
  expect(sent[0].headers()['x-xsrf-token']).toBe('tok_1');
  expect(sent[0].postDataJSON()).toEqual(
    savedSettings({ interval_hours: 3, timeout_seconds: 45, alert_email: null, version: '7' }),
  );

  // The next save names the version this one returned.
  await settings.timeout.fill('50');
  await settings.save.click();
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
  expect(sent[1].postDataJSON().version).toBe('8');
});

test('confirms the save for 8 seconds, then the bar goes', async ({ page, api }) => {
  api.on('PUT', '/admin/v1/settings', (request) => ({
    json: { ...request.postDataJSON(), version: '8' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');
  await settings.save.click();
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);

  await page.clock.fastForward(7_000);
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
  await page.clock.fastForward(1_000);

  await expect(settings.saveBar).toHaveCount(0);
});

test('ends the confirmation at the next edit', async ({ page, api }) => {
  api.on('PUT', '/admin/v1/settings', (request) => ({
    json: { ...request.postDataJSON(), version: '8' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');
  await settings.save.click();
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);

  await settings.retention.fill('12');

  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
  // The timer of the confirmation that ended does not take the bar away.
  await page.clock.fastForward(8_000);
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
});

test('confirms a second save for its own 8 seconds', async ({ page, api }) => {
  api.on('PUT', '/admin/v1/settings', (request) => ({
    json: { ...request.postDataJSON(), version: '8' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');
  await settings.save.click();
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
  await page.clock.fastForward(5_000);
  await settings.timeout.fill('50');
  await settings.save.click();
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);

  // The first save's 8 seconds pass; the second's confirmation stays.
  await page.clock.fastForward(4_000);
  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
  await page.clock.fastForward(4_000);

  await expect(settings.saveBar).toHaveCount(0);
});

test('opens Runs from the confirmation', async ({ page, api }) => {
  api.on('PUT', '/admin/v1/settings', (request) => ({
    json: { ...request.postDataJSON(), version: '8' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');
  await settings.save.click();

  await settings.viewRuns.click();

  await expect(page).toHaveURL(/\/runs$/);
});

test('keeps the entries and the bar when the save fails, and gives a reference', async ({
  page,
  api,
}) => {
  api.on('PUT', '/admin/v1/settings', () => ({
    status: 503,
    json: { code: 'internal_error', trace_id: 'req_5c1e0f7a' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');

  await settings.save.click();

  await expect(settings.failure).toHaveAttribute('role', 'alert');
  await expect(settings.failure).toContainText(
    'Keryx couldn’t save your changes just now. They’re still here: try again, and if it keeps failing, send the reference below to whoever runs Keryx.',
  );
  await expect(settings.failure).toContainText('Reference req_5c1e0f7a');
  await expect(settings.timeout).toHaveValue('45');
  await expect(settings.timeout).toBeEnabled();
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
  await expect(settings.save).toBeEnabled();
});

test('overwrites nothing when the settings changed elsewhere, and reloads them on request', async ({
  page,
  api,
}) => {
  let reads = 0;
  api.on('GET', '/admin/v1/settings', () => {
    reads += 1;
    return {
      json: reads === 1 ? savedSettings() : savedSettings({ timeout_seconds: 60, version: '9' }),
    };
  });
  api.on('PUT', '/admin/v1/settings', () => ({
    status: 409,
    json: { code: 'settings_changed' },
  }));
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('45');

  await settings.save.click();

  await expect(settings.changed).toHaveAttribute('role', 'alert');
  await expect(settings.changed).toContainText('Settings changed elsewhere');
  await expect(settings.changed).toContainText(
    'Someone saved Settings after you opened them, so nothing was overwritten. Your changes are still here: note them, reload to see the latest settings, then make them again.',
  );
  await expect(settings.timeout).toHaveValue('45');
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
  expect(reads).toBe(1);

  await settings.reload.click();

  await expect(settings.timeout).toHaveValue('60');
  await expect(settings.changed).toHaveCount(0);
  await expect(settings.saveBar).toHaveCount(0);
  expect(reads).toBe(2);
});
