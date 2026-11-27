import { Injectable } from '@angular/core';
import type { SessionApi, StaffProfile } from '@kx/api';
import { Observable } from 'rxjs';

export class SessionError extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
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
          throw new SessionError(response.status, error?.code);
        }
        subscriber.next(await response.json());
        subscriber.complete();
      };
      void load().catch((error) => subscriber.error(error));
      return () => controller.abort();
    });
  }

  signInUrl(returnPath: string): string {
    const query = new URLSearchParams({ return: returnPath });
    return '/admin/v1/auth/sign-in?' + query;
  }
}
