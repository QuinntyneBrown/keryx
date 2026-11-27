import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end specs for the admin app (AGENTS.md, "E2E (Playwright) and CI"). No backend and no
 * database run: the shared fixture in e2e/fixtures answers every API request and fails a spec on
 * an unanswered request, a request off localhost or a page error.
 *
 * Only behaviour specs run by default. Visual, responsive, accessibility and heap sweeps carry the
 * tag @sweep and run only when a person asks: KX_SWEEPS=1 npx playwright test --grep @sweep.
 * `npm run e2e` runs every behaviour spec; CI runs `npm run e2e:critical`, the one test per
 * feature tagged @critical.
 */
const ADMIN_PORT = Number(process.env['KX_ADMIN_PORT'] ?? 4320);
// CI serves the build the previous step made; a person's run compiles with `ng serve`.
const builtApp = process.env['KX_SERVE_BUILT'] === '1';
const sweeps = process.env['KX_SWEEPS'] === '1';

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: 0,
  // The runner has four cores and the specs wait on the browser more than they compute.
  workers: process.env['CI'] ? 4 : undefined,
  reporter: process.env['CI'] ? [['github'], ['list']] : 'list',
  grepInvert: sweeps ? undefined : /@sweep/,
  use: {
    ...devices['Desktop Chrome'],
    trace: 'retain-on-failure',
  },
  // Chromium only (AGENTS.md).
  projects: [
    {
      name: 'admin',
      testDir: 'e2e/admin',
      use: { browserName: 'chromium', baseURL: `http://localhost:${ADMIN_PORT}` },
    },
  ],
  webServer: [
    {
      command: builtApp
        ? `node e2e/static-server.mjs admin ${ADMIN_PORT}`
        : `npx ng serve admin --port ${ADMIN_PORT}`,
      url: `http://localhost:${ADMIN_PORT}`,
      reuseExistingServer: !process.env['CI'],
      timeout: 180_000,
    },
  ],
});
