// Acceptance Test
// Traces to: L2-040
// Description: A signed-in person who opens an address that does not exist sees Not found in the shell

import { expect, test } from '../fixtures/test';
import { NotFoundPage } from '../page-objects/admin/not-found.page';
import { SignInPage } from '../page-objects/admin/sign-in.page';

test.describe('Not found', () => {
  // L2-040 AC4: the Not found screen, inside the shell, with a link to Overview.
  test(
    'shows Not found in the shell with a link to Overview',
    { tag: '@critical' },
    async ({ page }) => {
      const screen = new NotFoundPage(page);

      await screen.open('/no-such-screen');

      expect(screen.path()).toBe('/no-such-screen');
      await expect.poll(() => screen.title()).toBe('Page not found · Keryx');
      await expect(screen.heading).toHaveText('Page not found');
      await expect(screen.text).toHaveText(
        'There’s nothing at this address. It may have been mistyped, or the link is out of date.',
      );
      await expect(screen.sidebar).toBeVisible();
      await expect(screen.currentLinks).toHaveCount(0);
      await expect(screen.overviewLink).toHaveText('Go to overview');
      await expect(screen.overviewLink).toHaveAttribute('href', '/');
    },
  );

  test('follows Go to overview without loading the document again', async ({ page }) => {
    const screen = new NotFoundPage(page);
    await screen.open('/no-such-screen/deeper');
    await screen.markDocument();

    await screen.overviewLink.click();

    await expect.poll(() => screen.path()).toBe('/');
    await expect(screen.heading).not.toHaveText('Page not found');
    expect(await screen.documentWasKept()).toBe(true);
  });

  // L2-039 AC1: an unknown address is still behind the guard; a signed-out visitor signs in first.
  test('sends a signed-out visitor to Sign in, not to Not found', async ({ page, api }) => {
    api.on('GET', '/admin/v1/me', () => ({ status: 401 }));
    const signIn = new SignInPage(page);

    await signIn.open('/no-such-screen');

    await expect(signIn.heading).toHaveText('Sign in to Keryx');
    await expect(page).toHaveURL(/\/sign-in\?return=%2Fno-such-screen/);
  });
});
