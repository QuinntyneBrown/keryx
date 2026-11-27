import { test as base, expect, type ConsoleMessage } from '@playwright/test';
import { ApiHandlers } from './api-handlers';
import {
  issuePage,
  LAST_14_RUNS,
  latestPage,
  OVERVIEW_REGIONS,
  overviewStatus,
  runPage,
} from './overview';

export { expect };
export type { ApiHandler, ApiHandlers, ApiReply } from './api-handlers';

/** Requests to these path prefixes go to the Keryx API, which never runs in a spec. */
const BACKEND = ['/admin/v1/', '/v1/', '/health/'];
const LOCAL_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]']);

/**
 * Chromium logs every 4xx or 5xx response, and every request that got no answer, as a console
 * error. For a backend request that is what a handler chose, not a fault of the page; an
 * unanswered one is reported below.
 */
function isAnsweredBackendStatus(message: ConsoleMessage): boolean {
  const text = message.text();
  const { url } = message.location();
  if (!url) return false;
  if (
    !text.startsWith('Failed to load resource: the server responded with a status of') &&
    text !== 'Failed to load resource: net::ERR_FAILED'
  ) {
    return false;
  }
  const { pathname } = new URL(url);
  return BACKEND.some((prefix) => pathname.startsWith(prefix));
}

interface Fixtures {
  /** The answers to the app's API requests; register them before the app sends the request. */
  api: ApiHandlers;
}

/**
 * The shared fixture every spec imports `test` and `expect` from (AGENTS.md, "E2E"). It:
 *
 * - answers each backend request (`/admin/v1/**`, `/v1/**`, `/health/**`) from the `api` handlers;
 * - fails the spec on an unanswered backend request, on any request off localhost (blocked) and
 *   on a page error (an uncaught exception, or an error the app logs to the console); the
 *   browser's own log of a status a handler answered is not one.
 *
 * A failure names what happened, never a body or a query string.
 */
export const test = base.extend<Fixtures>({
  api: async ({}, use) => {
    const handlers = new ApiHandlers();
    handlers.on('GET', '/admin/v1/me', () => ({
      json: { name: 'Daniel Adeyemi', initials: 'DA', role: 'Owner' },
    }));
    // Overview opens at `/`, where sign-in and the shell land.
    handlers.on('GET', '/admin/v1/overview/status', () => ({ json: overviewStatus() }));
    handlers.on('GET', '/admin/v1/runs', () => ({ json: runPage(LAST_14_RUNS, 2418) }));
    handlers.on('GET', '/admin/v1/articles', () => ({ json: latestPage() }));
    handlers.on('GET', '/admin/v1/regions', () => ({ json: { items: OVERVIEW_REGIONS } }));
    handlers.on('GET', '/admin/v1/issues', () => ({ json: issuePage() }));
    await use(handlers);
  },

  page: async ({ page, api }, use) => {
    const problems: string[] = [];

    page.on('pageerror', (error) => problems.push(`page error: ${error.message}`));
    page.on('console', (message) => {
      if (message.type() !== 'error' || isAnsweredBackendStatus(message)) return;
      problems.push(`console error: ${message.text()}`);
    });

    await page.context().route('**/*', async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      const what = `${request.method()} ${url.origin}${url.pathname}`;
      if (!LOCAL_HOSTS.has(url.hostname)) {
        problems.push(`request off localhost: ${what}`);
        await route.abort('blockedbyclient');
        return;
      }
      if (BACKEND.some((prefix) => url.pathname.startsWith(prefix))) {
        if (!(await api.answer(route))) {
          problems.push(`unanswered request: ${what}`);
          await route.fulfill({ status: 501, body: 'Not answered by the e2e fixture' });
        }
        return;
      }
      await route.fallback();
    });

    await use(page);

    expect(
      problems,
      'The page made a request no handler answered, left localhost or threw',
    ).toEqual([]);
  },
});
