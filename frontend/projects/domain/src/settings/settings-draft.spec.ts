import type { SettingsDto } from '@kx/api';
import { draftOf, settingsEqual, settingsOf, type SettingsDraft } from './settings-draft';

const saved: SettingsDto = {
  interval_hours: 6,
  timeout_seconds: 30,
  publish_threshold: 0.6,
  topics: ['fitness', 'running'],
  exclusions: ['casino'],
  focus_areas: ['Toronto'],
  include_ontario_wide: true,
  include_national: false,
  merge_duplicates: true,
  retention_months: 24,
  alert_email: null,
  alert_failure_streak: 3,
  alert_on_run_failure: true,
  daily_digest: true,
  version: '1',
};

describe('settingsEqual', () => {
  it('holds for the entries of the saved settings', () => {
    expect(settingsEqual(draftOf(saved), draftOf(saved))).toBe(true);
  });

  // One row per field: a change to it alone makes the entries differ.
  const changes: [string, Partial<SettingsDraft>][] = [
    ['interval', { interval_hours: 3 }],
    ['timeout', { timeout_seconds: '45' }],
    ['threshold', { publish_threshold: 0.65 }],
    ['topics, reordered', { topics: ['running', 'fitness'] }],
    ['topics, one more', { topics: ['fitness', 'running', 'yoga'] }],
    ['exclusions', { exclusions: [] }],
    ['focus areas', { focus_areas: ['Toronto', 'Ajax'] }],
    ['Ontario-wide', { include_ontario_wide: false }],
    ['national', { include_national: true }],
    ['merge', { merge_duplicates: false }],
    ['retention', { retention_months: '12' }],
    ['e-mail', { alert_email: 'ops@keryx.ca' }],
    ['streak', { alert_failure_streak: '5' }],
    ['run failure', { alert_on_run_failure: false }],
    ['digest', { daily_digest: false }],
  ];
  for (const [what, change] of changes) {
    it(`fails when the ${what} differs`, () => {
      expect(settingsEqual({ ...draftOf(saved), ...change }, draftOf(saved))).toBe(false);
    });
  }

  it('reads no e-mail as an empty entry', () => {
    expect(draftOf(saved).alert_email).toBe('');
  });
});

describe('settingsOf', () => {
  it('sends the saved settings back as they came', () => {
    expect(settingsOf(draftOf(saved), '1')).toEqual(saved);
  });

  it('sends typed numbers as numbers, an empty e-mail as none and the loaded version', () => {
    const sent = settingsOf(
      {
        ...draftOf(saved),
        timeout_seconds: ' 45 ',
        retention_months: '12',
        alert_failure_streak: '5',
        alert_email: '  ',
      },
      '8',
    );
    expect([
      sent.timeout_seconds,
      sent.retention_months,
      sent.alert_failure_streak,
      sent.alert_email,
      sent.version,
    ]).toEqual([45, 12, 5, null, '8']);
    expect(settingsOf({ ...draftOf(saved), alert_email: ' ops@keryx.ca ' }, '1').alert_email).toBe(
      'ops@keryx.ca',
    );
  });
});
