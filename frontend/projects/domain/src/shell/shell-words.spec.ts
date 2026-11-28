import { collectionRecurrence, failingSourcesLabel } from '../public-api';

describe('failingSourcesLabel', () => {
  // L2-042 AC3: the Runs badge is named by the count, in the singular for one.
  it.each([
    [2, '2 failing sources'],
    [1, '1 failing source'],
    [12, '12 failing sources'],
    [0, null],
  ])('names %i failing sources %p', (count, label) => {
    expect(failingSourcesLabel(count)).toBe(label);
  });
});

describe('collectionRecurrence', () => {
  // L2-042 AC1: the schedule in words, in the zone's city.
  it.each([
    [6, 'America/Toronto', 'Every 6 hours, Toronto time'],
    [1, 'America/Toronto', 'Every hour, Toronto time'],
    [24, 'America/Toronto', 'Every 24 hours, Toronto time'],
    [2, 'America/New_York', 'Every 2 hours, New York time'],
  ])('says every %i hours in %s as %p', (hours, zone, words) => {
    expect(collectionRecurrence(hours, zone)).toBe(words);
  });
});
