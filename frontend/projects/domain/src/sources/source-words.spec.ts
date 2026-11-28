import {
  formatSourceAddress,
  sourceHealthLabel,
  sourceKindLabel,
  sourceKindOf,
  sourceLastFetched,
  sourceSiteLink,
  sourcesNoResults,
  sourcesRange,
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

  // L2-062 AC3: the reason, and the streak once more than one run has failed.
  it.each([
    ['not_found', 1, '404 Not Found'],
    ['timeout', 4, 'Timeout · 4 runs'],
    ['timeout', 2, 'Timeout · 2 runs'],
    ['blocked_by_robots', 1, 'Blocked by robots.txt'],
    ['refused', 1, 'Refused'],
    ['invalid_content', 1, 'Unreadable content'],
    ['unsafe_address', 1, 'Not a public address'],
    ['not_found', 1200, '404 Not Found · 1,200 runs'],
  ] as const)('words failing for %s over %d runs', (reason, failures, text) => {
    expect(sourceHealthLabel('failing', 0, reason, failures)).toEqual({
      tone: 'danger',
      prefix: 'Failing: ',
      text,
    });
  });

  it('words a reason it does not know as Failing', () => {
    expect(sourceHealthLabel('failing', 0, 'gremlins', 3)).toEqual({
      tone: 'danger',
      prefix: null,
      text: 'Failing',
    });
  });
});

describe('sourcesRange', () => {
  it.each([
    [1, 64, null, 'Showing 1–12 of 64 sources, failing first.'],
    [6, 64, null, 'Showing 61–64 of 64 sources, failing first.'],
    [1, 1, null, 'Showing 1–1 of 1 source, failing first.'],
    [1, 0, null, 'Showing 0 sources, failing first.'],
    [1, 23, 'rss', 'Showing 1–12 of 23 RSS sources, failing first.'],
    [1, 1, 'news_query', 'Showing 1–1 of 1 news query source, failing first.'],
    [1, 3, 'website', 'Showing 1–3 of 3 website sources, failing first.'],
  ] as const)('page %i of %i sources of kind %s', (page, total, kind, expected) => {
    expect(sourcesRange(page, 12, total, kind)).toBe(expected);
  });
});

describe('sourceKindOf', () => {
  it.each([
    ['rss', 'rss'],
    ['news_query', 'news_query'],
    ['fax', null],
    ['RSS', null],
    [null, null],
  ] as const)('reads %s as %s', (value, kind) => {
    expect(sourceKindOf(value)).toBe(kind);
  });
});

describe('sourcesNoResults', () => {
  // L2-062 AC5, as pages/sources/no-results.html words it for Eventbrite.
  it.each([
    [
      'eventbrite',
      64,
      'No Eventbrite sources',
      'None of the 64 sources is an Eventbrite organiser. Show every type, or add one.',
    ],
    [
      'rss',
      1200,
      'No RSS sources',
      'None of the 1,200 sources is an RSS feed. Show every type, or add one.',
    ],
    [
      'website',
      2,
      'No website sources',
      'None of the 2 sources is a website. Show every type, or add one.',
    ],
    [
      'instagram',
      1,
      'No Instagram sources',
      'The one source isn’t an Instagram account. Show every type, or add one.',
    ],
    [
      'news_query',
      5,
      'No news query sources',
      'None of the 5 sources is a news query. Show every type, or add one.',
    ],
  ] as const)('%s among %i', (kind, all, title, text) => {
    expect(sourcesNoResults(kind, all)).toEqual({ title, text });
  });
});

describe('sourceLastFetched', () => {
  const now = new Date('2026-11-28T14:46:00Z');
  it.each([
    ['2026-11-28T11:01:00Z', 'Today, 6:01 AM'],
    ['2026-11-27T22:15:00Z', 'Yesterday, 5:15 PM'],
    [null, 'Not yet'],
  ])('%s', (at, expected) => {
    expect(sourceLastFetched(at, now, 'America/Toronto')).toBe(expected);
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

describe('sourceSiteLink', () => {
  // L2-063 AC4: only http(s) opens, never a script or a query.
  it.each([
    ['https://redeemedrun.ca/feed', { host: 'redeemedrun.ca', url: 'https://redeemedrun.ca/feed' }],
    ['http://www.Example.ca/a?b=1', { host: 'example.ca', url: 'http://www.example.ca/a?b=1' }],
    ['javascript:alert(1)', null],
    ['data:text/html,hi', null],
    ['ftp://files.example.ca/feed', null],
    ['"church" AND fitness near:Toronto', null],
  ] as const)('%s', (address, link) => {
    expect(sourceSiteLink(address)).toEqual(link);
  });
});
