import {
  formatSourceAddress,
  sourceHealthLabel,
  sourceKindLabel,
  sourcesSummary,
} from '../public-api';

describe('formatSourceAddress', () => {
  // The address drops the scheme and "www." (design system, source: Content).
  it.each([
    ['rss', 'https://redeemedrun.ca/feed', 'redeemedrun.ca/feed'],
    ['website', 'https://www.covenantcycling.ca/rides', 'covenantcycling.ca/rides'],
    ['website', 'http://WWW.Example.ca/', 'example.ca'],
    ['rss', 'https://shepherds5k.ca/feed?format=rss', 'shepherds5k.ca/feed?format=rss'],
    [
      'news_query',
      '"church" AND (fitness OR run) near:Toronto',
      '"church" AND (fitness OR run) near:Toronto',
    ],
    ['news_query', 'https://not-a-url-for-a-query', 'https://not-a-url-for-a-query'],
    ['rss', 'not a url', 'not a url'],
  ] as const)('shows a %s address %s as %s', (kind, address, expected) => {
    expect(formatSourceAddress(kind, address)).toBe(expected);
  });
});

describe('sourceKindLabel', () => {
  it.each([
    ['rss', 'RSS'],
    ['website', 'Website'],
    ['instagram', 'Instagram'],
    ['eventbrite', 'Eventbrite'],
    ['news_query', 'News query'],
  ] as const)('names %s %s', (kind, expected) => {
    expect(sourceKindLabel(kind)).toBe(expected);
  });
});

describe('sourceHealthLabel', () => {
  // The hidden prefix gives the category only when the text does not already say it.
  it.each([
    ['healthy', 0, { tone: 'healthy', prefix: null, text: 'Healthy' }],
    ['needs_look', 0, { tone: 'warning', prefix: null, text: 'Needs a look' }],
    ['needs_look', 6, { tone: 'warning', prefix: 'Needs a look: ', text: '6 held for review' }],
    ['needs_look', 1, { tone: 'warning', prefix: 'Needs a look: ', text: '1 held for review' }],
    ['failing', 0, { tone: 'danger', prefix: null, text: 'Failing' }],
  ] as const)('words %s with %d held', (health, held, expected) => {
    expect(sourceHealthLabel(health, held)).toEqual(expected);
  });
});

describe('sourcesSummary', () => {
  it.each([
    [{ all: 0, failing: 0, needsLook: 0 }, 'No sources yet.'],
    [{ all: 1, failing: 0, needsLook: 0 }, '1 source across the GTA'],
    [
      { all: 64, failing: 2, needsLook: 1 },
      '64 sources across the GTA · 2 failing · 1 need a look',
    ],
    [{ all: 3, failing: 0, needsLook: 2 }, '3 sources across the GTA · 2 need a look'],
    [{ all: 3, failing: 3, needsLook: 0 }, '3 sources across the GTA · 3 failing'],
    [{ all: 1234, failing: 0, needsLook: 0 }, '1,234 sources across the GTA'],
  ])('summarises %o as %s', (counts, expected) => {
    expect(sourcesSummary(counts)).toBe(expected);
  });
});
