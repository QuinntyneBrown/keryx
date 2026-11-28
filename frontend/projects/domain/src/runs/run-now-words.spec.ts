import { collectionStartedText } from '../public-api';

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
