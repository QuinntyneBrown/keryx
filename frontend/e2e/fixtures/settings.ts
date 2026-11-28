/** Settings as `GET /admin/v1/settings` serves them: the values the Settings mocks show. */
export interface SettingsBody {
  interval_hours: number;
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

export function savedSettings(changes: Partial<SettingsBody> = {}): SettingsBody {
  return {
    interval_hours: 6,
    timeout_seconds: 30,
    publish_threshold: 0.6,
    topics: [
      'fitness',
      'running',
      'strength',
      'sports ministry',
      'wellness',
      'church',
      'faith',
      'nutrition',
    ],
    exclusions: ['casino', 'supplement ads', 'sponsored'],
    focus_areas: [
      'Toronto',
      'Mississauga',
      'Brampton',
      'Markham',
      'Vaughan',
      'Oakville',
      'Richmond Hill',
      'Pickering',
      'Ajax',
    ],
    include_ontario_wide: true,
    include_national: false,
    merge_duplicates: true,
    retention_months: 24,
    alert_email: 'ops@keryx.ca',
    alert_failure_streak: 3,
    alert_on_run_failure: true,
    daily_digest: true,
    version: '7',
    ...changes,
  };
}
