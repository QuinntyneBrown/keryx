import { canRetryFailedSources, retryStartedNotice } from '../public-api';

describe('canRetryFailedSources', () => {
  // L2-059 AC4: no retry for a run with nothing that failed; L2-008 AC5: a failed run starts anew.
  it.each([
    ['completed_with_issues', 2, true],
    ['succeeded', 0, false],
    ['completed_with_issues', 0, false],
    ['failed', 0, true],
    ['failed', 64, true],
    ['running', 2, false],
  ] as const)('a %s run with %i failed sources: %s', (outcome, failed, offered) => {
    expect(canRetryFailedSources({ outcome, sources_failed: failed })).toBe(offered);
  });
});

describe('retryStartedNotice', () => {
  // L2-059 AC1, as the run-toast info mock words it.
  it.each([
    [
      2,
      ['Redeemed Run Club Toronto', 'Covenant Cycling Collective'],
      'Retrying 2 failed sources',
      'Redeemed Run Club Toronto and Covenant Cycling Collective. Results are added to this run.',
    ],
    [
      1,
      ['Kingdom Lifters'],
      'Retrying 1 failed source',
      'Kingdom Lifters. Results are added to this run.',
    ],
    [
      4,
      ['A', 'B', 'C', 'D'],
      'Retrying 4 failed sources',
      'A, B and 2 more. Results are added to this run.',
    ],
    [3, [], 'Retrying 3 failed sources', 'Results are added to this run.'],
  ] as const)('%i failed named %j', (failed, names, title, text) => {
    expect(retryStartedNotice(failed, names)).toEqual({ title, text });
  });
});
