import { SessionError } from './http-session-api';
import { inject } from '@angular/core';
import { type CanMatchFn, Router } from '@angular/router';
import { SESSION_API } from '@kx/api';
import { catchError, map, of } from 'rxjs';

export const staffGuard: CanMatchFn = () => {
  const router = inject(Router);
  const returnPath = router.currentNavigation()?.extractedUrl.toString() ?? '/';
  return inject(SESSION_API)
    .getMe()
    .pipe(
      map(() => true),
      catchError((error: unknown) => {
        if (error instanceof SessionError && error.status === 403 && error.code === 'not_staff') {
          return of(
            router.createUrlTree(['/forbidden'], {
              queryParams: { ref: error.reference, return: returnPath },
            }),
          );
        }
        const state =
          error instanceof SessionError && error.status === 401
            ? error.code === 'session_expired'
              ? 'session-expired'
              : undefined
            : 'error';
        return of(
          router.createUrlTree(['/sign-in'], { queryParams: { return: returnPath, state } }),
        );
      }),
    );
};
