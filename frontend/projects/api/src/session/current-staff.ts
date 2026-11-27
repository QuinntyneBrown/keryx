import { InjectionToken, type Signal } from '@angular/core';
import type { StaffProfile } from './session-api';

/** Who is signed in, as last read from the API; undefined until the staff guard has read it. */
export interface CurrentStaff {
  readonly profile: Signal<StaffProfile | undefined>;
  set(profile: StaffProfile): void;
  clear(): void;
}

export const CURRENT_STAFF = new InjectionToken<CurrentStaff>('Keryx current staff member');
