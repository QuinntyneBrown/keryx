import {
  collectionEndedNotice,
  collectionRunningLine,
  collectionStartedText,
  type EndedRun,
} from '../public-api';

describe('collectionStartedText', () => {
  // L2-047 AC1: how many sources the run checks, and about how long the last run took.
  it.each([
    [64, 252_000, 'Checking 64 sources. This usually takes about 4 minutes.'],
    [64, 90_000, 'Checking 64 sources. This usually takes about 2 minutes.'],
    [64, 30_000, 'Checking 64 sources. This usually takes about 1 minute.'],
    [64, 10_000, 'Checking 64 sources. This usually takes about 1 minute.'],
    [1, 60_000, 'Checking 1 source. This usually takes about 1 minute.'],
    [64, null, 'Checking 64 sources.'],
    [0, null, 'Checking 0 sources.'],
  ] as const)('%i sources after a run of %s ms read %s', (sources, ms, words) => {
    expect(collectionStartedText(sources, ms)).toBe(words);
  });
});

describe('collectionRunningLine', () => {
  it.each([
    ['2026-11-28T14:46:00Z', 'America/Toronto', 'A collection is running now · started 9:46 AM'],
    ['2026-11-28T17:05:00Z', 'America/Toronto', 'A collection is running now · started 12:05 PM'],
    ['2026-11-28T14:46:00Z', 'UTC', 'A collection is running now · started 2:46 PM'],
  ])('a run started at %s in %s reads %s', (startedAt, zone, words) => {
    expect(collectionRunningLine(startedAt, zone)).toBe(words);
  });
});

describe('collectionEndedNotice', () => {
  const source = (name: string | null, severity = 'error') => ({
    severity,
    subject_type: 'source',
    subject_name: name,
  });
  const run = (fields: Partial<EndedRun>): EndedRun => ({
    outcome: 'succeeded',
    added: 47,
    sources_failed: 0,
    sources_total: 64,
    stages: [],
    issues: [],
    ...fields,
  });

  // L2-076 AC2–AC4, as the collection-toast mocks word them.
  it.each([
    [
      'every source collected',
      run({}),
      {
        tone: 'success',
        title: 'Collection finished',
        text: '47 new articles from 64 sources.',
        action: 'view',
      },
    ],
    [
      'one new article from one source',
      run({ added: 1, sources_total: 1 }),
      {
        tone: 'success',
        title: 'Collection finished',
        text: '1 new article from 1 source.',
        action: 'view',
      },
    ],
    [
      'issues but no failing source',
      run({ outcome: 'completed_with_issues', issues: [source('Held items', 'warning')] }),
      {
        tone: 'success',
        title: 'Collection finished',
        text: '47 new articles from 64 sources.',
        action: 'view',
      },
    ],
    [
      'two failing sources',
      run({
        outcome: 'completed_with_issues',
        sources_failed: 2,
        issues: [source('Redeemed Run Club Toronto'), source('Covenant Cycling Collective')],
      }),
      {
        tone: 'warning',
        title: 'Collection finished with 2 failing sources',
        text: '47 new articles. Redeemed Run Club Toronto and Covenant Cycling Collective didn’t respond.',
        action: 'view',
      },
    ],
    [
      'one failing source',
      run({
        outcome: 'completed_with_issues',
        added: 1,
        sources_failed: 1,
        issues: [source('Kingdom Lifters')],
      }),
      {
        tone: 'warning',
        title: 'Collection finished with 1 failing source',
        text: '1 new article. Kingdom Lifters didn’t respond.',
        action: 'view',
      },
    ],
    [
      'four failing sources; warnings, other subjects and nameless ones left out',
      run({
        outcome: 'completed_with_issues',
        sources_failed: 4,
        issues: [
          { severity: 'error', subject_type: 'run', subject_name: 'Store' },
          source('A', 'warning'),
          source(null),
          source('B'),
          source('C'),
          source('D'),
        ],
      }),
      {
        tone: 'warning',
        title: 'Collection finished with 4 failing sources',
        text: '47 new articles. B, C and 2 more didn’t respond.',
        action: 'view',
      },
    ],
    [
      'failing sources with no names',
      run({ outcome: 'completed_with_issues', sources_failed: 2, issues: [] }),
      {
        tone: 'warning',
        title: 'Collection finished with 2 failing sources',
        text: '47 new articles.',
        action: 'view',
      },
    ],
    [
      'the store stage failed',
      run({ outcome: 'failed', added: 0, stages: [{ stage: 'store', status: 'failed' }] }),
      {
        tone: 'danger',
        title: 'Collection failed',
        text: 'Keryx couldn’t reach its database, so nothing was stored. The API is still serving the previous run.',
        action: 'retry',
      },
    ],
    [
      'stopped for another reason',
      run({ outcome: 'failed', added: 0, stages: [{ stage: 'fetch', status: 'failed' }] }),
      {
        tone: 'danger',
        title: 'Collection failed',
        text: 'The run stopped before anything was stored. The API is still serving the previous run.',
        action: 'retry',
      },
    ],
  ] as const)('%s', (_case, ended, notice) => {
    expect(collectionEndedNotice(ended)).toEqual(notice);
  });
});
