import { InjectionToken } from '@angular/core';
import type { Observable } from 'rxjs';

export interface StaffProfile {
  name: string;
  initials: string;
  role: string;
}

export interface SessionApi {
  getMe(): Observable<StaffProfile>;
  signOut(signal: AbortSignal): Promise<void>;
  signInUrl(returnPath: string, selectAccount?: boolean): string;
}

export const SESSION_API = new InjectionToken<SessionApi>('Keryx staff session');
