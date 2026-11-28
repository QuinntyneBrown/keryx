import {
  canRetryFailedSources,
  failedSources,
  retryStartedNotice,
  summarizeRetry,
} from '../public-api';

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

describe('summarizeRetry', () => {
  const redeemed = { name: 'Redeemed Run Club Toronto', kind: 'source_not_found' };
  const covenant = { name: 'Covenant Cycling Collective', kind: 'source_timeout' };
  const kingdom = { name: 'Kingdom Lifters', kind: 'source_blocked' };
  const temple = { name: 'Temple Fit', kind: 'source_invalid' };

  // L2-059 AC2, AC3, as the run-toast mocks word them; the other counts follow them.
  it.each([
    [
      'one of two recovered',
      [redeemed, covenant],
      [redeemed],
      {
        tone: 'warning',
        title: '1 of 2 sources recovered',
        text: 'Covenant Cycling Collective responded. Redeemed Run Club Toronto still returns 404.',
        action: 'Edit source',
      },
    ],
    [
      'two of four recovered',
      [redeemed, covenant, kingdom, temple],
      [kingdom, temple],
      {
        tone: 'warning',
        title: '2 of 4 sources recovered',
        text: 'Redeemed Run Club Toronto and Covenant Cycling Collective responded. Kingdom Lifters is still blocked and Temple Fit still answers with nothing Keryx can read.',
        action: 'Edit sources',
      },
    ],
    [
      'none of two recovered',
      [redeemed, covenant],
      [redeemed, covenant],
      {
        tone: 'danger',
        title: 'Both sources failed again',
        text: 'Redeemed Run Club Toronto returned 404 and Covenant Cycling Collective timed out.',
        action: 'Edit sources',
      },
    ],
    [
      'the one failed again',
      [kingdom],
      [kingdom],
      {
        tone: 'danger',
        title: 'The source failed again',
        text: 'Kingdom Lifters was blocked.',
        action: 'Edit source',
      },
    ],
    [
      'none of three recovered',
      [redeemed, covenant, temple],
      [redeemed, covenant, temple],
      {
        tone: 'danger',
        title: 'All 3 sources failed again',
        text: 'Redeemed Run Club Toronto returned 404, Covenant Cycling Collective timed out and 1 more failed.',
        action: 'Edit sources',
      },
    ],
    [
      'all recovered',
      [redeemed, covenant],
      [],
      {
        tone: 'success',
        title: 'All 2 sources recovered',
        text: 'Redeemed Run Club Toronto and Covenant Cycling Collective responded.',
        action: null,
      },
    ],
    [
      'the one recovered',
      [temple],
      [],
      {
        tone: 'success',
        title: 'The source recovered',
        text: 'Temple Fit responded.',
        action: null,
      },
    ],
  ] as const)('%s', (_case, before, after, summary) => {
    expect(summarizeRetry(before, after)).toEqual(summary);
  });
});

describe('failedSources', () => {
  const issue = (severity: string, subject_type: string, subject_name: string | null) => ({
    severity,
    kind: 'source_timeout',
    subject_type,
    subject_id: subject_type === 'source' ? 'src_a' : null,
    subject_name,
  });

  it.each([
    [
      'an error about a named source',
      [issue('error', 'source', 'A')],
      [{ name: 'A', kind: 'source_timeout', id: 'src_a' }],
    ],
    ['a warning about a source', [issue('warning', 'source', 'A')], []],
    ['an error about a stage', [issue('error', 'stage', 'Store')], []],
    ['an error about a source with no name', [issue('error', 'source', null)], []],
  ] as const)('%s', (_case, issues, failed) => {
    expect(failedSources(issues)).toEqual(failed);
  });
});
