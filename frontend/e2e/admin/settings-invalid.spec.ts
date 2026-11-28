// Acceptance Test
// Traces to: L2-071 AC3; L2-070 AC5
// Description: Saving entries out of range sends nothing. An error summary counts the fields to
// fix and links each line to its field, focus moves to it, each field shows its own message and
// aria-invalid, the save bar says how many need fixing, and no entry is cleared.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('sends nothing, and says which two fields to fix and how', async ({ page, api }) => {
  let saves = 0;
  api.on('PUT', '/admin/v1/settings', () => {
    saves += 1;
    return { status: 500 };
  });
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.option('3 hours').click();
  await settings.timeout.fill('0');
  await settings.alertEmail.fill('ops@keryx');

  await settings.save.click();

  await expect(settings.summary).toHaveAttribute('role', 'alert');
  await expect(settings.summary).toBeFocused();
  await expect(settings.summaryTitle).toHaveText('Fix 2 fields to save your changes');
  await expect(settings.summaryLinks).toHaveText([
    'Timeout per source: enter a timeout between 5 and 120 seconds.',
    'E-mail: enter an address like name@example.com.',
  ]);
  await expect(settings.timeout).toHaveAttribute('aria-invalid', 'true');
  await expect(settings.fieldError(settings.timeout)).toHaveText(
    'Enter a timeout between 5 and 120 seconds.',
  );
  await expect(settings.timeout).toHaveAccessibleDescription(
    'Enter a timeout between 5 and 120 seconds.',
  );
  await expect(settings.alertEmail).toHaveAttribute('aria-invalid', 'true');
  await expect(settings.fieldError(settings.alertEmail)).toHaveText(
    'Enter an e-mail address like name@example.com.',
  );
  await expect(settings.alertEmail).toHaveAccessibleDescription(
    'Enter an e-mail address like name@example.com.',
  );
  await expect(settings.retention).not.toHaveAttribute('aria-invalid');
  await expect(settings.saveBarText).toHaveText('2 fields need fixing before you can save.');
  await expect(settings.saveBarText).toHaveClass(/save-bar__text--danger/);
  // No entry is cleared.
  await expect(settings.timeout).toHaveValue('0');
  await expect(settings.alertEmail).toHaveValue('ops@keryx');
  await expect(settings.pressedOptions).toHaveText(['3 hours']);
  expect(saves).toBe(0);

  await settings.summaryLinks.nth(1).click();

  await expect(settings.alertEmail).toBeFocused();
});

test('clears the errors with the entries on Discard', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();
  await settings.timeout.fill('0');
  await settings.save.click();
  await expect(settings.summary).toBeFocused();

  await settings.discard.click();

  await expect(settings.summary).toHaveCount(0);
  await expect(settings.timeout).toHaveValue('30');
  await expect(settings.timeout).not.toHaveAttribute('aria-invalid');
  await settings.timeout.fill('45');
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');
});

for (const [name, fill, message, summary] of [
  [
    'Keep articles for',
    (settings: SettingsPage) => settings.retention.fill('121'),
    'Enter a period between 1 and 120 months.',
    'Keep articles for: enter a period between 1 and 120 months.',
  ],
  [
    'Alert when a source fails',
    (settings: SettingsPage) => settings.alertStreak.fill('0'),
    'Enter a number of runs between 1 and 20.',
    'Alert when a source fails: enter a number of runs between 1 and 20.',
  ],
] as const) {
  test(`says how to fix “${name}” when Enter is pressed in it`, async ({ page }) => {
    const settings = new SettingsPage(page);
    await settings.open();
    await fill(settings);
    const control = page.getByLabel(name);

    await control.press('Enter');

    await expect(settings.summaryTitle).toHaveText('Fix 1 field to save your changes');
    await expect(settings.summaryLinks).toHaveText([summary]);
    await expect(settings.fieldError(control)).toHaveText(message);
    await expect(control).toHaveAttribute('aria-invalid', 'true');
    await expect(settings.saveBarText).toHaveText('1 field needs fixing before you can save.');

    await settings.summaryLinks.first().press('Enter');

    await expect(control).toBeFocused();
  });
}
