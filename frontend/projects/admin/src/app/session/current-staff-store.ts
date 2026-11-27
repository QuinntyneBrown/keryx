import { Injectable, signal } from '@angular/core';
import type { CurrentStaff, StaffProfile } from '@kx/api';

/** Holds the signed-in person in memory only; nothing goes to storage or the address. */
@Injectable()
export class CurrentStaffStore implements CurrentStaff {
  private readonly current = signal<StaffProfile | undefined>(undefined);
  readonly profile = this.current.asReadonly();

  set(profile: StaffProfile): void {
    this.current.set(profile);
  }

  clear(): void {
    this.current.set(undefined);
  }
}
