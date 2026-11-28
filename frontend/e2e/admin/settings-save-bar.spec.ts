// Acceptance Test
// Traces to: L2-071 AC1, AC2; L2-070 AC1
// Description: While any value differs from the saved one, a bar fixed to the bottom of the
// viewport says there are unsaved changes, with Discard and Save changes. When every value is
// the saved one again the bar goes; Discard returns every field to its saved value. Account is
// not part of the form and never makes it dirty.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('shows the save bar while a value differs, and hides it when it is the saved one again', async ({
  page,
}) => {
  const settings = new SettingsPage(page);
  await settings.open();
  await expect(settings.saveBar).toHaveCount(0);

  await settings.timeout.fill('45');

  await expect(page.getByRole('region', { name: 'Unsaved changes' })).toBeVisible();
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
  await expect(settings.discard).toBeVisible();
  await expect(settings.save).toBeVisible();
  const bar = await settings.saveBar.boundingBox();
  expect(Math.round((bar?.y ?? 0) + (bar?.height ?? 0))).toBe(page.viewportSize()?.height);

  await settings.timeout.fill('30');

  await expect(settings.saveBar).toHaveCount(0);
});

for (const [what, change] of [
  ['an interval', (settings: SettingsPage) => settings.option('3 hours').click()],
  [
    'the threshold',
    async (settings: SettingsPage) => {
      await settings.threshold.focus();
      await settings.threshold.press('ArrowLeft');
    },
  ],
  [
    'the switch “Ontario-wide organisations”',
    (settings: SettingsPage) => settings.switchNamed('Ontario-wide organisations').press('Space'),
  ],
  [
    'the switch “National Canadian news”',
    (settings: SettingsPage) => settings.switchNamed('National Canadian news').press('Space'),
  ],
  [
    'the switch “Match by URL and similar headline (90%)”',
    (settings: SettingsPage) =>
      settings.switchNamed('Match by URL and similar headline (90%)').press('Space'),
  ],
  [
    'the switch “When a whole run fails”',
    (settings: SettingsPage) => settings.switchNamed('When a whole run fails').press('Space'),
  ],
  [
    'the switch “Daily digest of held articles”',
    (settings: SettingsPage) =>
      settings.switchNamed('Daily digest of held articles').press('Space'),
  ],
  ['the e-mail', (settings: SettingsPage) => settings.alertEmail.fill('alerts@keryx.ca')],
  ['the retention', (settings: SettingsPage) => settings.retention.fill('12')],
  ['the failure streak', (settings: SettingsPage) => settings.alertStreak.fill('5')],
] as const) {
  test(`shows the save bar when ${what} changes`, async ({ page }) => {
    const settings = new SettingsPage(page);
    await settings.open();

    await change(settings);

    await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
  });
}

test('returns every field to its saved value on Discard', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.option('3 hours').click();
  await settings.timeout.fill('45');
  await settings.threshold.focus();
  await settings.threshold.press('ArrowRight');
  await settings.switchNamed('National Canadian news').press('Space');
  await settings.switchNamed('Daily digest of held articles').press('Space');
  await settings.retention.fill('12');
  await settings.alertEmail.fill('alerts@keryx.ca');
  await settings.alertStreak.fill('5');

  await settings.discard.click();

  await expect(settings.saveBar).toHaveCount(0);
  await expect(settings.pressedOptions).toHaveText(['6 hours']);
  await expect(settings.scheduleLine).toHaveText('cron: 0 */6 * * * · next 12:00 PM');
  await expect(settings.timeout).toHaveValue('30');
  await expect(settings.threshold).toHaveValue('0.6');
  await expect(settings.thresholdValue).toHaveText('0.60');
  await expect(settings.switchNamed('National Canadian news')).not.toBeChecked();
  await expect(settings.switchNamed('Daily digest of held articles')).toBeChecked();
  await expect(settings.retention).toHaveValue('24');
  await expect(settings.alertEmail).toHaveValue('ops@keryx.ca');
  await expect(settings.alertStreak).toHaveValue('3');
});
