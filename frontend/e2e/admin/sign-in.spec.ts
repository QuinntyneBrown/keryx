import { expect, test } from '../fixtures/test';
import { SignInPage } from '../page-objects/admin/sign-in.page';

// L2-039. A signed-out visitor starts at the protected screen they intend to use.
test(
  'preserves the requested route through sign-in',
  { tag: '@critical' },
  async ({ page, api }) => {
    api.on('GET', '/admin/v1/me', () => ({ status: 401 }));
    let requestedReturn: string | null = null;
    api.on('GET', '/admin/v1/auth/sign-in', (request) => {
      requestedReturn = new URL(request.url()).searchParams.get('return');
      return { status: 200, body: Buffer.from('Provider handoff') };
    });
    const signIn = new SignInPage(page);
    await signIn.open('/articles?region=toronto');
    await expect(signIn.heading).toHaveText('Sign in to Keryx');
    await expect(signIn.navigation).toHaveCount(0);
    await expect(page).toHaveURL(/\/sign-in\?return=/);
    await signIn.button.click();
    await expect.poll(() => requestedReturn).toBe('/articles?region=toronto');
  },
);
