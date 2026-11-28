import type { CanDeactivateFn } from '@angular/router';

/** A screen that may hold unsaved work, and asks before it is left (L2-071 AC6). */
export interface LeavesSafely {
  canLeave(): boolean | Promise<boolean>;
}

/** Asks the screen whether it may be left; choosing to stay cancels the navigation. */
export const unsavedChangesGuard: CanDeactivateFn<LeavesSafely> = (screen) => screen.canLeave();
