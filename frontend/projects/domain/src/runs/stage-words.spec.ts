import {
  formatStageDuration,
  PIPELINE_STAGES,
  stageName,
  stageState,
  stageStatusLabel,
  stageSummary,
} from './stage-words';

describe('stageName', () => {
  it('names the six stages in order', () => {
    expect(PIPELINE_STAGES.map(stageName)).toEqual([
      'Fetch sources',
      'Parse & extract',
      'Classify relevance',
      'Deduplicate',
      'Store',
      'Publish to API',
    ]);
  });

  it('shows a stage Keryx does not know as itself, even one named like a property', () => {
    expect(stageName('enrich')).toBe('enrich');
    expect(stageName('toString')).toBe('toString');
  });
});

describe('stageStatusLabel and stageState', () => {
  it.each([
    ['done', 'Done', 'ok'],
    ['done_with_warnings', 'Done with warnings', 'warn'],
    ['failed', 'Failed', 'failed'],
    ['running', 'Running', 'running'],
    ['not_run', 'Not run', 'skipped'],
    ['toString', 'Not run', 'skipped'],
  ])('%s reads %s as %s', (status, word, state) => {
    expect(stageStatusLabel(status)).toBe(word);
    expect(stageState(status)).toBe(state);
  });
});

describe('formatStageDuration', () => {
  it.each([
    [168_000, '2:48'],
    [6_000, '0:06'],
    [6_999, '0:06'],
    [615_000, '10:15'],
  ])('%i ms reads %s', (ms, words) => {
    expect(formatStageDuration(ms)).toBe(words);
  });
});

describe('stageSummary', () => {
  it.each([
    ['fetch', { sources_ok: 62, sources_failed: 2 }, '62 of 64 sources fetched · 2 failed'],
    ['fetch', { sources_ok: 64, sources_failed: 0 }, '64 of 64 sources fetched'],
    ['parse', { items: 189 }, '189 candidate items from feeds, pages and posts'],
    ['parse', { items: 1 }, '1 candidate item from feeds, pages and posts'],
    [
      'classify',
      { relevant: 53, held: 6, off_topic: 130 },
      '53 relevant · 6 held for review · 130 off-topic',
    ],
    ['deduplicate', { duplicates: 18, rejected: 0 }, '18 duplicates merged with existing articles'],
    [
      'deduplicate',
      { duplicates: 1, rejected: 2 },
      '1 duplicate merged with existing articles · 2 rejected',
    ],
    ['store', { added: 47 }, 'Saved to articles table'],
    ['publish', { published: 47 }, '47 new articles available at /v1/articles'],
    ['publish', { published: 1 }, '1 new article available at /v1/articles'],
    ['publish', {}, '0 new articles available at /v1/articles'],
    ['enrich', { items: 3 }, ''],
  ])('%s with %o reads %s', (stage, counts, words) => {
    expect(stageSummary(stage, counts)).toBe(words);
  });
});
