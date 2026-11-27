import { Injectable } from '@angular/core';
import type { SessionApi, StaffProfile } from '@kx/api';
import { Observable } from 'rxjs';

export class SessionError extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
    readonly reference?: string,
  ) {
    super('Session request failed');
  }
}

@Injectable()
export class HttpSessionApi implements SessionApi {
  getMe(): Observable<StaffProfile> {
    return new Observable((subscriber) => {
      const controller = new AbortController();
      const load = async () => {
        const response = await fetch('/admin/v1/me', {
          credentials: 'include',
          signal: controller.signal,
          cache: 'no-store',
        });
        if (!response.ok) {
          const error = await response.json().catch(() => null);
          throw new SessionError(response.status, error?.code, error?.trace_id);
        }
        subscriber.next(await response.json());
        subscriber.complete();
      };
      void load().catch((error) => subscriber.error(error));
      return () => controller.abort();
    });
  }

  async signOut(signal: AbortSignal): Promise<void> {
    const options = { credentials: 'include', cache: 'no-store', signal } as const;
    const token = await fetch('/admin/v1/auth/antiforgery', options);
    if (token.status === 401) return;
    if (!token.ok) throw new SessionError(token.status);
    const body = await token.json();
    if (typeof body.request_token !== 'string') throw new SessionError(400);
    const ended = await fetch('/admin/v1/auth/sign-out', {
      ...options,
      method: 'POST',
      headers: { 'X-XSRF-TOKEN': body.request_token },
    });
    if (!ended.ok && ended.status !== 401) throw new SessionError(ended.status);
  }

  signInUrl(returnPath: string, selectAccount = false): string {
    const query = new URLSearchParams({ return: returnPath });
    if (selectAccount) query.set('prompt', 'select_account');
    return '/admin/v1/auth/sign-in?' + query;
  }
}
