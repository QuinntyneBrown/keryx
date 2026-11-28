import type { SettingsDto } from '@kx/api';
import { draftOf, type SettingsDraft } from './settings-draft';
import { fixFieldsBar, fixFieldsTitle, settingsValidation } from './settings-validation';

const saved: SettingsDto = {
  interval_hours: 6,
  timeout_seconds: 30,
  publish_threshold: 0.6,
  topics: ['fitness'],
  exclusions: [],
  focus_areas: ['Toronto'],
  include_ontario_wide: true,
  include_national: false,
  merge_duplicates: true,
  retention_months: 24,
  alert_email: 'ops@keryx.ca',
  alert_failure_streak: 3,
  alert_on_run_failure: true,
  daily_digest: true,
  version: '1',
};

const fields = (change: Partial<SettingsDraft>) =>
  settingsValidation({ ...draftOf(saved), ...change }).map((error) => error.field);

describe('settingsValidation', () => {
  // The ranges of L2-070 AC5, as the server's validator holds them: one row per bound.
  const cases: [Partial<SettingsDraft>, string[]][] = [
    [{}, []],
    [{ timeout_seconds: '5' }, []],
    [{ timeout_seconds: '120' }, []],
    [{ timeout_seconds: ' 45 ' }, []],
    [{ timeout_seconds: '4' }, ['timeout']],
    [{ timeout_seconds: '121' }, ['timeout']],
    [{ timeout_seconds: '0' }, ['timeout']],
    [{ timeout_seconds: '' }, ['timeout']],
    [{ timeout_seconds: '3.5' }, ['timeout']],
    [{ timeout_seconds: 'ten' }, ['timeout']],
    [{ retention_months: '1' }, []],
    [{ retention_months: '120' }, []],
    [{ retention_months: '0' }, ['retention']],
    [{ retention_months: '121' }, ['retention']],
    [{ alert_failure_streak: '1' }, []],
    [{ alert_failure_streak: '20' }, []],
    [{ alert_failure_streak: '0' }, ['alert-after']],
    [{ alert_failure_streak: '21' }, ['alert-after']],
    [{ alert_email: '' }, []],
    [{ alert_email: '  ' }, []],
    [{ alert_email: 'ops@keryx' }, ['alert-email']],
    [{ alert_email: 'ops keryx@keryx.ca' }, ['alert-email']],
    [{ alert_email: `ops@${'k'.repeat(247)}.ca` }, []],
    [{ alert_email: `ops@${'k'.repeat(248)}.ca` }, ['alert-email']],
    [
      {
        timeout_seconds: '0',
        alert_email: 'ops@keryx',
        retention_months: '0',
        alert_failure_streak: '0',
      },
      ['timeout', 'retention', 'alert-email', 'alert-after'],
    ],
  ];
  for (const [change, expected] of cases) {
    it(`finds [${expected.join(', ')}] in ${JSON.stringify(change).slice(0, 60)}`, () => {
      expect(fields(change)).toEqual(expected);
    });
  }

  it('words the timeout and e-mail as L2-071 AC3 does', () => {
    expect(
      settingsValidation({ ...draftOf(saved), timeout_seconds: '0', alert_email: 'ops@keryx' }),
    ).toEqual([
      {
        field: 'timeout',
        message: 'Enter a timeout between 5 and 120 seconds.',
        summary: 'Timeout per source: enter a timeout between 5 and 120 seconds.',
      },
      {
        field: 'alert-email',
        message: 'Enter an e-mail address like name@example.com.',
        summary: 'E-mail: enter an address like name@example.com.',
      },
    ]);
  });
});

describe('fix fields words', () => {
  it('counts one field and several', () => {
    expect([fixFieldsTitle(1), fixFieldsTitle(2)]).toEqual([
      'Fix 1 field to save your changes',
      'Fix 2 fields to save your changes',
    ]);
    expect([fixFieldsBar(1), fixFieldsBar(2)]).toEqual([
      '1 field needs fixing before you can save.',
      '2 fields need fixing before you can save.',
    ]);
  });
});
