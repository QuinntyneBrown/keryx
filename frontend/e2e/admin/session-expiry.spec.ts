// Acceptance Test
// Traces to: L2-041
// Description: A session that ends while the app is open leads to Sign in, keeping the route

import { expect, test } from '../fixtures/test';
import { SettingsPage } from '../page-objects/admin/settings.page';
import { SignInPage } from '../page-objects/admin/sign-in.page';

// L2-041 AC2: `session_expired` on the next request shows the session-expired state and keeps
// the route to return to.
test('sends an expired session to Sign in, keeping the route it was going to', async ({
  page,
  api,
}) => {
  let expired = false;
  api.on('GET', '/admin/v1/me', () =>
    expired
      ? { status: 401, json: { code: 'session_expired' } }
      : { json: { name: 'Daniel Adeyemi', initials: 'DA', role: 'Owner' } },
  );
  const settings = new SettingsPage(page);
  await page.goto('/');
  await settings.navLink.waitFor();
  expired = true;

  await settings.navLink.click();

  const signIn = new SignInPage(page);
  await expect(signIn.notice).toContainText('Your session ended');
  expect(settings.path()).toBe('/sign-in');
  expect(settings.search().get('state')).toBe('session-expired');
  expect(settings.search().get('return')).toBe('/settings');
});
