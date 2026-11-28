// Acceptance Test
// Traces to: L2-070 AC1, AC2, AC3, AC4; L2-071 AC8
// Description: Settings opens with every saved value in five sections, then Account. "Run every"
// marks one choice pressed and reads its cron and next run; the threshold slider reads its value
// and what waits for review; each switch is a native checkbox whose state shows by position.
// A slow answer shows skeletons first, and a failed one says Settings didn't load.

import { expect, test } from '../fixtures/test';
import { OVERVIEW_NOW } from '../fixtures/overview';
import { savedSettings } from '../fixtures/settings';
import { SettingsPage } from '../page-objects/admin/settings.page';

test.beforeEach(async ({ page }) => {
  // 9:46 AM in Toronto, the mocks' time.
  await page.clock.setFixedTime(OVERVIEW_NOW);
});

test('opens with every saved value in five sections, then Account', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();

  await expect(settings.sectionHeadings).toHaveText([
    'Schedule',
    'Relevance',
    'Geography',
    'Storage',
    'Alerts',
    'Account',
  ]);
  await expect(settings.form).toHaveAttribute('novalidate', '');
  await expect(settings.runEveryOptions).toHaveText([
    '1 hour',
    '3 hours',
    '6 hours',
    '12 hours',
    'Day',
  ]);
  await expect(settings.pressedOptions).toHaveText(['6 hours']);
  await expect(settings.scheduleLine).toHaveText('cron: 0 */6 * * * · next 12:00 PM');
  await expect(settings.timeout).toHaveValue('30');
  await expect(settings.suffixes).toHaveText(['seconds', 'months', 'runs in a row']);
  await expect(settings.chips('Topics')).toHaveText(savedSettings().topics);
  await expect(settings.chips('Excluded terms')).toHaveText([
    'casino',
    'supplement ads',
    'sponsored',
  ]);
  await expect(settings.chips('Focus areas')).toHaveText(savedSettings().focus_areas);
  await expect(settings.threshold).toHaveValue('0.6');
  await expect(settings.thresholdValue).toHaveText('0.60');
  await expect(settings.thresholdHelp).toHaveText(
    'Scores from 0.45 to 0.60 wait in Needs review. Below 0.45 are hidden.',
  );
  await expect(settings.switches).toHaveCount(5);
  for (const [name, on] of [
    ['Ontario-wide organisations', true],
    ['National Canadian news', false],
    ['Match by URL and similar headline (90%)', true],
    ['When a whole run fails', true],
    ['Daily digest of held articles', true],
  ] as const) {
    const control = page.getByRole('switch', { name });
    await expect(control).toHaveAttribute('type', 'checkbox');
    if (on) await expect(control).toBeChecked();
    else await expect(control).not.toBeChecked();
  }
  await expect(settings.retention).toHaveValue('24');
  await expect(settings.alertEmail).toHaveValue('ops@keryx.ca');
  await expect(settings.alertStreak).toHaveValue('3');
  await expect(settings.signOut).toBeVisible();
});

test('marks the chosen interval alone pressed and reads its cron and next run', async ({
  page,
}) => {
  const settings = new SettingsPage(page);
  await settings.open();

  await settings.option('3 hours').click();
  await expect(settings.pressedOptions).toHaveText(['3 hours']);
  await expect(settings.scheduleLine).toHaveText('cron: 0 */3 * * * · next 12:00 PM');

  await settings.option('1 hour').press('Enter');
  await expect(settings.pressedOptions).toHaveText(['1 hour']);
  await expect(settings.scheduleLine).toHaveText('cron: 0 * * * * · next 10:00 AM');

  await settings.option('Day').click();
  await expect(settings.pressedOptions).toHaveText(['Day']);
  await expect(settings.scheduleLine).toHaveText('cron: 0 0 * * * · next 12:00 AM');
});

test('moves the threshold with the arrow keys and says what waits for review', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();

  await settings.threshold.focus();
  await page.keyboard.press('ArrowRight');

  await expect(settings.threshold).toHaveValue('0.65');
  await expect(settings.thresholdValue).toHaveText('0.65');
  await expect(settings.thresholdHelp).toHaveText(
    'Scores from 0.45 to 0.65 wait in Needs review. Below 0.45 are hidden.',
  );
});

test('shows a switch state by the position of its thumb', async ({ page }) => {
  const settings = new SettingsPage(page);
  await settings.open();
  const national = page.getByRole('switch', { name: 'National Canadian news' });
  const off = await settings.thumbShift(national);

  await national.press('Space');

  await expect(national).toBeChecked();
  expect(await settings.thumbShift(national)).not.toBe(off);
});

test('shows skeleton sections while the answer is slow, then the form', async ({ page, api }) => {
  let answer: () => void = () => undefined;
  const answered = new Promise<void>((resolve) => (answer = resolve));
  api.on('GET', '/admin/v1/settings', async () => {
    await answered;
    return { json: savedSettings() };
  });
  const settings = new SettingsPage(page);
  await settings.open();

  await expect(settings.body).toHaveAttribute('aria-busy', 'true');
  await expect(settings.skeletonSections).toHaveCount(5);
  await expect(settings.loadingStatus).toHaveText('Loading settings…');

  answer();

  await expect(settings.timeout).toHaveValue('30');
  await expect(settings.skeletonSections).toHaveCount(0);
  await expect(settings.body).not.toHaveAttribute('aria-busy');
});

test('says Settings didn’t load with a reference, and loads again on Try again', async ({
  page,
  api,
}) => {
  let fail = true;
  api.on('GET', '/admin/v1/settings', () =>
    fail
      ? {
          status: 503,
          json: { code: 'internal_error', trace_id: 'req_22d9a6b4' },
        }
      : { json: savedSettings() },
  );
  const settings = new SettingsPage(page);
  await settings.open();

  await expect(settings.errorTitle).toHaveText('Settings didn’t load');
  await expect(settings.errorReference).toHaveText('Reference req_22d9a6b4');
  await expect(settings.form).toHaveCount(0);

  fail = false;
  await settings.tryAgain.click();

  await expect(settings.timeout).toHaveValue('30');
  await expect(settings.error).toHaveCount(0);
});
