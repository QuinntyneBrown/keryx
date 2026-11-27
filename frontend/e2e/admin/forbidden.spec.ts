import { expect, test } from '../fixtures/test';
import { ForbiddenPage } from '../page-objects/admin/forbidden.page';

test('refuses a non-staff account and signs it out before switching accounts', async ({
  page,
  api,
}) => {
  api.on('GET', '/admin/v1/me', () => ({
    status: 403,
    json: { code: 'not_staff', trace_id: 'example-ref' },
  }));
  api.on('GET', '/admin/v1/auth/antiforgery', () => ({ json: { request_token: 'test-token' } }));
  let verified = false;
  api.on('POST', '/admin/v1/auth/sign-out', (request) => {
    verified = request.headers()['x-xsrf-token'] === 'test-token';
    return { status: verified ? 204 : 400 };
  });
  let prompt: string | null = null;
  api.on('GET', '/admin/v1/auth/sign-in', (request) => {
    prompt = new URL(request.url()).searchParams.get('prompt');
    return { status: 200, body: Buffer.from('Account chooser') };
  });
  await page.goto('/articles');
  const forbidden = new ForbiddenPage(page);
  await expect(forbidden.heading).toHaveText('This account can’t open Keryx admin');
  await expect(forbidden.navigation).toHaveCount(0);
  await expect(forbidden.reference).toHaveText('Reference example-ref');
  await forbidden.switchAccount();
  await expect.poll(() => prompt).toBe('select_account');
  expect(verified).toBe(true);
});
