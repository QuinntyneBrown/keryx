import {
  articleStatusLabel,
  articleStatusTone,
  formatRelativeAge,
  formatRelevance,
  organizationLabel,
  safeHttpUrl,
  titleLabel,
} from '../public-api';

describe('formatRelativeAge', () => {
  const now = new Date('2026-11-27T15:00:00Z');

  // Minutes under an hour, hours under a day, otherwise days with a thousands separator (L2-053 AC7).
  it.each([
    ['2026-11-27T15:00:00Z', '0m ago'],
    ['2026-11-27T14:59:01Z', '0m ago'],
    ['2026-11-27T14:59:00Z', '1m ago'],
    ['2026-11-27T14:00:01Z', '59m ago'],
    ['2026-11-27T14:00:00Z', '1h ago'],
    ['2026-11-26T15:00:01Z', '23h ago'],
    ['2026-11-26T15:00:00Z', '1d ago'],
    ['2023-08-11T15:00:00Z', '1,204d ago'],
    // A time a little ahead of the browser's clock reads as now, never as negative.
    ['2026-11-27T15:02:00Z', '0m ago'],
  ])('reads %s as %s', (at, expected) => {
    expect(formatRelativeAge(at, now)).toBe(expected);
  });
});

describe('formatRelevance', () => {
  it.each([
    [0.94, '0.94'],
    [0.6, '0.60'],
    [1, '1.00'],
    [0, '0.00'],
    [0.5, '0.50'],
  ])('shows %s as %s', (score, expected) => {
    expect(formatRelevance(score)).toBe(expected);
  });
});

describe('articleStatusLabel and articleStatusTone', () => {
  it.each([
    ['published', 'Published', 'success'],
    ['needs_review', 'Needs review', 'warning'],
    ['hidden', 'Hidden', null],
  ] as const)('names %s %s with tone %s', (status, label, tone) => {
    expect(articleStatusLabel(status)).toBe(label);
    expect(articleStatusTone(status)).toBe(tone);
  });
});

describe('titleLabel and organizationLabel', () => {
  it('reads a missing title as "Untitled post" and a missing organization as "Unknown"', () => {
    expect(titleLabel(null)).toBe('Untitled post');
    expect(titleLabel('')).toBe('Untitled post');
    expect(titleLabel('Sunrise run')).toBe('Sunrise run');
    expect(organizationLabel(null)).toBe('Unknown');
    expect(organizationLabel('LifePoint Fitness')).toBe('LifePoint Fitness');
  });
});

describe('safeHttpUrl', () => {
  // A source link opens only as http(s) (AGENTS.md; L2-053 AC5).
  it.each([
    ['https://redeemedrun.ca/n/1', 'https://redeemedrun.ca/n/1'],
    ['http://redeemedrun.ca/n/1', 'http://redeemedrun.ca/n/1'],
    ['HTTPS://Example.ca/A', 'HTTPS://Example.ca/A'],
    ['javascript:alert(1)', null],
    ['JavaScript:alert(1)', null],
    ['data:text/html,<b>x</b>', null],
    ['ftp://example.ca/file', null],
    ['//example.ca/n/1', null],
    ['/n/1', null],
    ['not a url', null],
    ['', null],
  ])('allows %s as %s', (url, expected) => {
    expect(safeHttpUrl(url)).toBe(expected);
  });
});
