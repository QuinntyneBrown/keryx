import type { Request, Route } from '@playwright/test';

/**
 * What a handler answers: a status (200 unless given) and a JSON body, raw bytes (`body`), or no
 * body; or, with `abort`, no answer at all, as when the network drops (the request fails with
 * status 0).
 */
export interface ApiReply {
  status?: number;
  json?: unknown;
  body?: Buffer;
  headers?: Record<string, string>;
  abort?: boolean;
}

/** Answers one API request. It receives the request as the app sent it. */
export type ApiHandler = (request: Request) => ApiReply | Promise<ApiReply>;

interface Registration {
  method: string;
  path: string | RegExp;
  handler: ApiHandler;
}

/**
 * The answers to the admin app's API requests. A guard or an endpoint every screen calls gets its
 * default answer in `registrations` when its journey adds it; a spec overrides one with `on`. The
 * app calls nothing yet, so there is no default.
 */
export class ApiHandlers {
  private readonly registrations: Registration[] = [];

  /** Answers `method` requests to `path` (exact pathname, or a pattern over it) with `handler`. */
  on(method: string, path: string | RegExp, handler: ApiHandler): void {
    this.registrations.unshift({ method: method.toUpperCase(), path, handler });
  }

  /** Fulfils the route from the first matching handler. Returns false when none matches. */
  async answer(route: Route): Promise<boolean> {
    const request = route.request();
    const { pathname } = new URL(request.url());
    const match = this.registrations.find(
      (r) =>
        r.method === request.method() &&
        (typeof r.path === 'string' ? r.path === pathname : r.path.test(pathname)),
    );
    if (!match) return false;
    const reply = await match.handler(request);
    if (reply.abort) {
      await route.abort('failed');
      return true;
    }
    await route.fulfill({
      status: reply.status ?? 200,
      headers: reply.headers,
      ...(reply.json === undefined ? {} : { json: reply.json }),
      ...(reply.body === undefined ? {} : { body: reply.body }),
    });
    return true;
  }
}
