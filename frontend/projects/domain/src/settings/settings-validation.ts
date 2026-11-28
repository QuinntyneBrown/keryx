import type { SettingsDraft } from './settings-draft';

/** A field the form checks before sending, by the id of its control. */
export type SettingsField = 'timeout' | 'retention' | 'alert-email' | 'alert-after';

/** What is wrong with one field: its own message, and the summary's line naming it. */
export interface SettingsError {
  field: SettingsField;
  message: string;
  summary: string;
}

const NUMBER = /^\d+$/;
// The server's rule (UpdateSettingsValidator): something, an @, something, a dot, something.
const ADDRESS = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

function within(entry: string, min: number, max: number): boolean {
  const value = entry.trim();
  return NUMBER.test(value) && Number(value) >= min && Number(value) <= max;
}

/**
 * The entries' errors in the order of the form (L2-071 AC3), by the ranges of L2-070 AC5. An
 * empty e-mail is allowed: then no alert is sent.
 */
export function settingsValidation(draft: SettingsDraft): SettingsError[] {
  const errors: SettingsError[] = [];
  if (!within(draft.timeout_seconds, 5, 120)) {
    errors.push({
      field: 'timeout',
      message: 'Enter a timeout between 5 and 120 seconds.',
      summary: 'Timeout per source: enter a timeout between 5 and 120 seconds.',
    });
  }
  if (!within(draft.retention_months, 1, 120)) {
    errors.push({
      field: 'retention',
      message: 'Enter a period between 1 and 120 months.',
      summary: 'Keep articles for: enter a period between 1 and 120 months.',
    });
  }
  const email = draft.alert_email.trim();
  if (email && (email.length > 254 || !ADDRESS.test(email))) {
    errors.push({
      field: 'alert-email',
      message: 'Enter an e-mail address like name@example.com.',
      summary: 'E-mail: enter an address like name@example.com.',
    });
  }
  if (!within(draft.alert_failure_streak, 1, 20)) {
    errors.push({
      field: 'alert-after',
      message: 'Enter a number of runs between 1 and 20.',
      summary: 'Alert when a source fails: enter a number of runs between 1 and 20.',
    });
  }
  return errors;
}

/** The summary's title: "Fix 2 fields to save your changes". */
export function fixFieldsTitle(count: number): string {
  return `Fix ${count} ${count === 1 ? 'field' : 'fields'} to save your changes`;
}

/** The save bar while fields need fixing: "2 fields need fixing before you can save." */
export function fixFieldsBar(count: number): string {
  return count === 1
    ? '1 field needs fixing before you can save.'
    : `${count} fields need fixing before you can save.`;
}
