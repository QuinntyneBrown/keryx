import {
  issueStreakLabel,
  runFailedFlag,
  runLineMeta,
  runOutcomeLabel,
  scheduleLine,
} from './run-history-words';

describe('issueStreakLabel', () => {
  it.each([
    ['error', 1, null],
    ['error', 2, 'Failing 2 runs in a row'],
    ['error', 4, 'Failing 4 runs in a row'],
    ['warning', 4, null],
  ] as const)('a %s over %i runs reads %s', (severity, runs, words) => {
    expect(issueStreakLabel(severity, runs)).toBe(words);
  });
});

describe('runOutcomeLabel', () => {
  it.each([
    ['succeeded', 'Succeeded'],
    ['completed_with_issues', 'Completed with issues'],
    ['failed', 'Failed'],
    ['running', 'Running'],
  ] as const)('%s reads %s', (outcome, word) => {
    expect(runOutcomeLabel(outcome)).toBe(word);
  });
});

describe('runLineMeta', () => {
  it.each([
    ['succeeded', 'schedule', 12, 231_000, '12 new · 3m 51s'],
    ['completed_with_issues', 'schedule', 47, 252_000, '47 new · 4m 12s'],
    ['succeeded', 'manual', 8, 220_000, '8 new · 3m 40s · manual'],
    ['failed', 'schedule', 0, 48_000, 'Failed · 0m 48s'],
    ['failed', 'manual', 0, 48_000, 'Failed · 0m 48s · manual'],
  ] as const)('a %s %s run adding %i in %i ms reads %s', (outcome, trigger, added, ms, words) => {
    expect(runLineMeta({ outcome, trigger, added, duration_ms: ms })).toBe(words);
  });
});

describe('runFailedFlag', () => {
  it.each([
    [0, null],
    [1, '1 failed'],
    [2, '2 failed'],
  ])('%i failed sources flag %s', (failed, flag) => {
    expect(runFailedFlag(failed)).toBe(flag);
  });
});

describe('scheduleLine', () => {
  const now = new Date('2026-11-28T14:46:00Z');

  it.each([
    [6, '2026-11-28T17:00:00Z', 'Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)'],
    [1, '2026-11-28T15:00:00Z', 'Scheduled every hour · next at 10:00 AM (in 14m)'],
    [24, '2026-11-29T05:00:00Z', 'Scheduled every 24 hours · next at 12:00 AM (in 14h 14m)'],
  ])('every %i hours, next at %s, reads %s', (interval_hours, next_run_at, words) => {
    expect(scheduleLine({ interval_hours, next_run_at, time_zone: 'America/Toronto' }, now)).toBe(
      words,
    );
  });
});
