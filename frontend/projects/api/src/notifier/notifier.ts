import { InjectionToken } from '@angular/core';

/** A notification: its tone, title, sentence and at most one action. */
export interface Notice {
  tone: 'success' | 'warning' | 'danger' | 'info';
  title: string;
  text?: string;
  action?: { label: string; run: () => void };
}

/** Shows notifications in the shell's toast region (L2-075). */
export interface Notifier {
  notify(notice: Notice): void;
}

export const NOTIFIER = new InjectionToken<Notifier>('Keryx notifier');
