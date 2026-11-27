import { InjectionToken } from '@angular/core';
import type { Observable } from 'rxjs';

export interface StaffProfile {
  name: string;
  initials: string;
  role: string;
}

export interface SessionApi {
  getMe(): Observable<StaffProfile>;
  signInUrl(returnPath: string): string;
}

export const SESSION_API = new InjectionToken<SessionApi>('Keryx staff session');
