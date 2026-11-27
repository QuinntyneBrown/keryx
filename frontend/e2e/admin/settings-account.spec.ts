// Acceptance Test
// Traces to: L2-041, L2-042
// Description: Staff see who is signed in under Settings › Account and sign out from there

import { expect, test } from '../fixtures/test';
import { SettingsPage } from '../page-objects/admin/settings.page';
import { SignInPage } from '../page-objects/admin/sign-in.page';

test.describe('Settings › Account', () => {
  // L2-041 AC3: "Sign out" ends the session on the server and shows the signed-out state.
  test(
    'signs out from the Account section and shows the signed-out state',
    { tag: '@critical' },
    async ({ page, api }) => {
      api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
      const tokens: (string | undefined)[] = [];
      api.on('POST', '/admin/v1/auth/sign-out', (request) => {
        tokens.push(request.headers()['x-xsrf-token']);
        return { status: 204 };
      });
      const settings = new SettingsPage(page);

      await settings.open();

      await expect(settings.heading).toHaveText('Settings');
      await expect(settings.accountHeading).toHaveText('Account');
      await expect(settings.signedInLabel).toHaveText('Signed in as');
      await expect(settings.signedInAs).toHaveText('Daniel Adeyemi · Owner');

      await settings.signOut.click();

      const signIn = new SignInPage(page);
      await expect(signIn.notice).toContainText('You’re signed out');
      expect(settings.path()).toBe('/sign-in');
      expect(settings.search().get('state')).toBe('signed-out');
      expect(tokens).toEqual(['tok_1']);
    },
  );

  test('keeps the person signed in and says so when sign-out fails', async ({ page, api }) => {
    api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'tok_1' } }));
    let attempts = 0;
    api.on('POST', '/admin/v1/auth/sign-out', () => {
      attempts += 1;
      return attempts === 1 ? { status: 503 } : { status: 204 };
    });
    const settings = new SettingsPage(page);
    await settings.open();

    await settings.signOut.click();

    await expect(settings.signOutFailure).toContainText('Couldn’t sign out');
    await expect(settings.signOutFailure).toHaveAttribute('role', 'alert');
    expect(settings.path()).toBe('/settings');
    await expect(settings.signOut).toBeEnabled();

    await settings.signOut.click();

    await expect.poll(() => settings.path()).toBe('/sign-in');
    expect(settings.search().get('state')).toBe('signed-out');
  });

  // L2-042 AC2: exactly one link is current, and not by colour alone.
  test('marks the Settings link as the current page', async ({ page }) => {
    const settings = new SettingsPage(page);

    await settings.open();

    await expect(settings.currentLinks).toHaveCount(1);
    await expect(settings.navLink).toHaveAttribute('aria-current', 'page');
    expect(await settings.styleOf(settings.navLink, 'font-weight')).toBe('500');
  });

  // L2-042 AC5: the screen's code is loaded on demand, from its navigation link.
  test('opens Settings from its navigation link without loading the document again', async ({
    page,
  }) => {
    const settings = new SettingsPage(page);
    await page.goto('/');
    await page.evaluate(() => document.documentElement.setAttribute('data-e2e-kept', ''));

    await settings.navLink.click();

    await expect(settings.heading).toHaveText('Settings');
    expect(settings.path()).toBe('/settings');
    expect(await page.evaluate(() => document.documentElement.hasAttribute('data-e2e-kept'))).toBe(
      true,
    );
  });
});
