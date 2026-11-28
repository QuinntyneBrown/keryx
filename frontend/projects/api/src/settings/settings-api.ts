import { InjectionToken } from '@angular/core';

/** The intervals staff may choose, in hours (L2-070 AC5). */
export type IntervalHours = 1 | 3 | 6 | 12 | 24;

/**
 * Settings as `GET` and `PUT /admin/v1/settings` carry them (L2-070). `version` is opaque: it is
 * sent back on a save, so a save cannot overwrite a change made elsewhere since (L2-071 AC7).
 */
export interface SettingsDto {
  interval_hours: IntervalHours;
  timeout_seconds: number;
  publish_threshold: number;
  topics: string[];
  exclusions: string[];
  focus_areas: string[];
  include_ontario_wide: boolean;
  include_national: boolean;
  merge_duplicates: boolean;
  retention_months: number;
  alert_email: string | null;
  alert_failure_streak: number;
  alert_on_run_failure: boolean;
  daily_digest: boolean;
  version: string;
}

/**
 * Settings (docs/detailed-designs/admin-app/manage-settings/). It rejects with an `ApiProblem`:
 * a save refuses `validation_failed` (an entry per field) or `settings_changed`.
 */
export interface SettingsApi {
  getSettings(signal: AbortSignal): Promise<SettingsDto>;
  saveSettings(settings: SettingsDto, signal: AbortSignal): Promise<SettingsDto>;
}

export const SETTINGS_API = new InjectionToken<SettingsApi>('Keryx settings');
