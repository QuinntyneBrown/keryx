import type { IntervalHours, SettingsDto } from '@kx/api';

/**
 * The Settings form's entries. A number typed into a field is held as typed, so an entry that is
 * not yet a valid number stays on screen to be corrected (L2-071 AC3).
 */
export interface SettingsDraft {
  interval_hours: IntervalHours;
  timeout_seconds: string;
  publish_threshold: number;
  topics: string[];
  exclusions: string[];
  focus_areas: string[];
  include_ontario_wide: boolean;
  include_national: boolean;
  merge_duplicates: boolean;
  retention_months: string;
  alert_email: string;
  alert_failure_streak: string;
  alert_on_run_failure: boolean;
  daily_digest: boolean;
}

/** The entries that show saved settings. */
export function draftOf(saved: SettingsDto): SettingsDraft {
  return {
    interval_hours: saved.interval_hours,
    timeout_seconds: String(saved.timeout_seconds),
    publish_threshold: saved.publish_threshold,
    topics: [...saved.topics],
    exclusions: [...saved.exclusions],
    focus_areas: [...saved.focus_areas],
    include_ontario_wide: saved.include_ontario_wide,
    include_national: saved.include_national,
    merge_duplicates: saved.merge_duplicates,
    retention_months: String(saved.retention_months),
    alert_email: saved.alert_email ?? '',
    alert_failure_streak: String(saved.alert_failure_streak),
    alert_on_run_failure: saved.alert_on_run_failure,
    daily_digest: saved.daily_digest,
  };
}

/** Whether two sets of entries are the same, list order included: the form is dirty when not. */
export function settingsEqual(a: SettingsDraft, b: SettingsDraft): boolean {
  return (Object.keys(a) as (keyof SettingsDraft)[]).every((field) => {
    const x = a[field];
    const y = b[field];
    return Array.isArray(x) && Array.isArray(y)
      ? x.length === y.length && x.every((value, i) => value === y[i])
      : x === y;
  });
}

/**
 * What a save sends for valid entries: numbers as numbers, an empty e-mail as none, and the
 * version the settings were loaded at (L2-071 AC7).
 */
export function settingsOf(draft: SettingsDraft, version: string): SettingsDto {
  const email = draft.alert_email.trim();
  return {
    interval_hours: draft.interval_hours,
    timeout_seconds: Number(draft.timeout_seconds),
    publish_threshold: draft.publish_threshold,
    topics: [...draft.topics],
    exclusions: [...draft.exclusions],
    focus_areas: [...draft.focus_areas],
    include_ontario_wide: draft.include_ontario_wide,
    include_national: draft.include_national,
    merge_duplicates: draft.merge_duplicates,
    retention_months: Number(draft.retention_months),
    alert_email: email || null,
    alert_failure_streak: Number(draft.alert_failure_streak),
    alert_on_run_failure: draft.alert_on_run_failure,
    daily_digest: draft.daily_digest,
    version,
  };
}
