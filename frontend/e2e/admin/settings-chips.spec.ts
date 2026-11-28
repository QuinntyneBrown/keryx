// Acceptance Test
// Traces to: L2-072 AC1–AC6; L2-071 AC1
// Description: Topics, Exclude and Focus areas are chip lists. Removing a chip makes the form
// dirty and moves focus to the next chip, or to Add after the last. Add opens a dialog for its
// list; a duplicate is refused, and a new value joins as a chip marked new until the form is
// saved, with focus back on Add. A value renders as text.

import type { Request } from '@playwright/test';
import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { savedSettings } from '../fixtures/settings';
import { AddKeywordDialog } from '../page-objects/admin/add-keyword.dialog';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('removes a chip and moves focus to the next one, or to Add after the last', async ({
  page,
}) => {
  const settings = new SettingsPage(page);
  await settings.open();

  await settings.remove('running').click();

  await expect(settings.chips('Topics')).toHaveText([
    'fitness',
    'strength',
    'sports ministry',
    'wellness',
    'church',
    'faith',
    'nutrition',
  ]);
  await expect(settings.remove('strength')).toBeFocused();
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');

  await settings.remove('sponsored').press('Enter');

  await expect(settings.chips('Excluded terms')).toHaveText(['casino', 'supplement ads']);
  await expect(settings.addTo('Exclude')).toBeFocused();

  await settings.discard.click();
  await expect(settings.chips('Topics')).toHaveText(savedSettings().topics);
  await expect(settings.chips('Excluded terms')).toHaveText(savedSettings().exclusions);
});

test('adds a topic through its dialog, marked new until saved, with focus back on Add', async ({
  page,
  api,
}) => {
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  const sent: Request[] = [];
  api.on('PUT', '/admin/v1/settings', (request) => {
    sent.push(request);
    return { json: { ...request.postDataJSON(), version: '8' } };
  });
  const settings = new SettingsPage(page);
  const dialog = new AddKeywordDialog(page);
  await settings.open();

  await settings.addTo('Topics').click();

  await expect(page.getByRole('dialog', { name: 'Add a topic' })).toHaveAttribute(
    'aria-modal',
    'true',
  );
  await expect(dialog.title).toHaveText('Add a topic');
  await expect(dialog.description).toHaveText(
    'It is added to the list; nothing changes until you save Settings.',
  );
  await expect(dialog.label).toHaveText('Topic');
  await expect(dialog.help).toHaveText(
    'A word or short phrase, like “pickleball” or “youth sports”. Items that mention it score higher.',
  );
  await expect(dialog.cancel).toBeVisible();
  await expect(dialog.submit).toHaveText('Add topic');
  await expect(dialog.field).toBeFocused();

  await dialog.field.fill(' Running ');
  await dialog.submit.click();

  await expect(dialog.error).toHaveText(
    '“Running” is already a topic. Enter a different word or phrase.',
  );
  await expect(dialog.field).toHaveAttribute('aria-invalid', 'true');
  // Nothing was added: the list below holds the saved topics and Pickleball alone.

  await dialog.field.fill('Pickleball');
  await dialog.field.press('Enter');

  await expect(dialog.root).toHaveCount(0);
  await expect(settings.chips('Topics')).toHaveText([...savedSettings().topics, 'Pickleball']);
  await expect(settings.newChips('Topics')).toHaveText(['Pickleball']);
  await expect(settings.addTo('Topics')).toBeFocused();
  await expect(settings.saveBarText).toHaveText('You have unsaved changes.');

  await settings.save.click();

  await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
  await expect(settings.newChips('Topics')).toHaveCount(0);
  expect(sent[0].postDataJSON().topics).toEqual([...savedSettings().topics, 'Pickleball']);
});

for (const [list, name, field, title, label, submit, value] of [
  [
    'Exclude',
    'Excluded terms',
    'exclusions',
    'Add an exclusion',
    'Term',
    'Add exclusion',
    'gambling',
  ],
  [
    'Focus areas',
    'Focus areas',
    'focus_areas',
    'Add a focus area',
    'Area',
    'Add focus area',
    'Hamilton',
  ],
] as const) {
  test(`opens “${title}” from ${list} and adds the value trimmed to that list`, async ({
    page,
    api,
  }) => {
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    const sent: Request[] = [];
    api.on('PUT', '/admin/v1/settings', (request) => {
      sent.push(request);
      return { json: { ...request.postDataJSON(), version: '8' } };
    });
    const settings = new SettingsPage(page);
    const dialog = new AddKeywordDialog(page);
    await settings.open();

    await settings.addTo(list).click();

    await expect(dialog.title).toHaveText(title);
    await expect(dialog.label).toHaveText(label);
    await expect(dialog.submit).toHaveText(submit);
    await dialog.field.fill(`  ${value} `);
    await dialog.submit.click();

    await expect(settings.newChips(name)).toHaveText([value]);
    await expect(settings.newChips('Topics')).toHaveCount(0);

    await settings.save.click();

    await expect(settings.saveBarText).toHaveText(/Saved at 9:46 AM/);
    expect(sent[0].postDataJSON()[field]).toEqual([...savedSettings()[field], value]);
  });
}

test('marks nothing new after reloading settings changed elsewhere', async ({ page, api }) => {
  let reads = 0;
  api.on('GET', '/admin/v1/settings', () => {
    reads += 1;
    return {
      json:
        reads === 1
          ? savedSettings()
          : savedSettings({ topics: [...savedSettings().topics, 'yoga'], version: '9' }),
    };
  });
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
  api.on('PUT', '/admin/v1/settings', () => ({ status: 409, json: { code: 'settings_changed' } }));
  const settings = new SettingsPage(page);
  const dialog = new AddKeywordDialog(page);
  await settings.open();
  await settings.addTo('Topics').click();
  await dialog.field.fill('yoga');
  await dialog.submit.click();
  await settings.save.click();

  await settings.reload.click();

  await expect(settings.chips('Topics')).toHaveText([...savedSettings().topics, 'yoga']);
  await expect(settings.newChips('Topics')).toHaveCount(0);
});

test('adds nothing on Cancel or Escape, and refuses a value too short', async ({ page }) => {
  const settings = new SettingsPage(page);
  const dialog = new AddKeywordDialog(page);
  await settings.open();

  await settings.addTo('Topics').click();
  await dialog.field.fill('yoga');
  await dialog.cancel.click();
  await expect(dialog.root).toHaveCount(0);
  await expect(settings.addTo('Topics')).toBeFocused();

  await settings.addTo('Topics').press('Enter');
  await dialog.field.fill(' a ');
  await dialog.submit.click();
  await expect(dialog.error).toHaveText('Enter 2 to 40 characters.');
  await page.keyboard.press('Escape');

  await expect(dialog.root).toHaveCount(0);
  await expect(settings.chips('Topics')).toHaveText(savedSettings().topics);
  await expect(settings.saveBar).toHaveCount(0);
});

test('renders a value as text', async ({ page }) => {
  const settings = new SettingsPage(page);
  const dialog = new AddKeywordDialog(page);
  await settings.open();

  await settings.addTo('Topics').click();
  await dialog.field.fill('<b>bold</b>');
  await dialog.submit.click();

  await expect(settings.newChips('Topics')).toHaveText(['<b>bold</b>']);
  await expect(settings.newChips('Topics').locator('b')).toHaveCount(0);
  await expect(settings.remove('<b>bold</b>')).toBeVisible();
});
