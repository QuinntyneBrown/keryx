import {
  formatClockTime,
  formatCountdown,
  formatDuration,
  runHeadline,
  runTone,
  type RunSummaryView,
} from '../public-api';

const run = (fields: Partial<RunSummaryView>): RunSummaryView => ({
  outcome: 'succeeded',
  failure_code: null,
  sources_ok: 64,
  sources_total: 64,
  ...fields,
});

describe('runHeadline and runTone', () => {
  // L2-043 AC1, AC2, AC4: the outcome in words, never colour alone (AC5).
  it.each([
    [run({}), 'Completed — all sources collected', 'success'],
    [
      run({ outcome: 'completed_with_issues', sources_ok: 62 }),
      'Completed with 2 failing sources',
      'warning',
    ],
    [
      run({ outcome: 'completed_with_issues', sources_ok: 63 }),
      'Completed with 1 failing source',
      'warning',
    ],
    [run({ outcome: 'completed_with_issues' }), 'Completed with issues', 'warning'],
    [
      run({ outcome: 'failed', failure_code: 'store_failed', sources_ok: 0 }),
      'Run failed — database unreachable',
      'danger',
    ],
    [
      run({ outcome: 'failed', failure_code: 'interrupted' }),
      'Run failed — interrupted by a restart',
      'danger',
    ],
    [
      run({ outcome: 'failed', failure_code: 'unexpected' }),
      'Run failed — stopped unexpectedly',
      'danger',
    ],
    [
      run({ outcome: 'failed', failure_code: 'fetch_failed' }),
      'Run failed — stopped unexpectedly',
      'danger',
    ],
    [run({ outcome: 'running' }), 'Collecting news now…', 'info'],
  ] as const)('%# reads %s', (shown, headline, tone) => {
    expect(runHeadline(shown)).toBe(headline);
    expect(runTone(shown.outcome)).toBe(tone);
  });
});

describe('formatDuration', () => {
  it.each([
    [252_000, '4m 12s'],
    [48_000, '0m 48s'],
    [59_999, '0m 59s'],
    [3_599_000, '59m 59s'],
    [3_600_000, '1h 0m'],
    [3_720_000, '1h 2m'],
  ])('%i ms reads %s', (ms, words) => {
    expect(formatDuration(ms)).toBe(words);
  });
});

describe('formatClockTime and formatCountdown', () => {
  const now = new Date('2026-11-28T14:46:00Z');

  it('reads a time in the zone it is given', () => {
    expect(formatClockTime('2026-11-28T17:00:00Z', 'America/Toronto')).toBe('12:00 PM');
  });

  it.each([
    ['2026-11-28T17:00:00Z', 'in 2h 14m'],
    ['2026-11-28T16:59:30Z', 'in 2h 14m'],
    ['2026-11-28T16:59:00Z', 'in 2h 13m'],
    ['2026-11-28T15:46:00Z', 'in 1h 0m'],
    ['2026-11-28T15:45:30Z', 'in 1h 0m'],
    ['2026-11-28T15:45:00Z', 'in 59m'],
    ['2026-11-28T14:47:00Z', 'in 1m'],
    ['2026-11-28T15:00:00Z', 'in 14m'],
    ['2026-11-28T14:46:59Z', 'in under a minute'],
    ['2026-11-28T14:40:00Z', 'due now'],
  ])('%s reads %s', (at, words) => {
    expect(formatCountdown(now, at)).toBe(words);
  });
});
