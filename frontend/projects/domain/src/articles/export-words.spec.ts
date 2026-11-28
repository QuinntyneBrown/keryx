import {
  exportFailedNotice,
  exportFileName,
  exportStartedNotice,
  exportTooLargeNotice,
} from '../public-api';

describe('exportFileName', () => {
  it.each([
    ['2026-11-27T15:00:00Z', 'articles-2026-11-27.csv'],
    // Toronto is UTC−5 in winter: its midnight is 05:00 UTC.
    ['2026-11-28T03:00:00Z', 'articles-2026-11-27.csv'],
    ['2026-11-28T04:59:59Z', 'articles-2026-11-27.csv'],
    ['2026-11-28T05:00:00Z', 'articles-2026-11-28.csv'],
    // And UTC−4 in summer: its midnight is 04:00 UTC.
    ['2026-07-01T03:59:59Z', 'articles-2026-06-30.csv'],
    ['2026-07-01T04:00:00Z', 'articles-2026-07-01.csv'],
    ['2027-01-01T04:59:59Z', 'articles-2026-12-31.csv'],
  ])('at %s is %s', (now, name) => {
    expect(exportFileName(new Date(now))).toBe(name);
  });
});

describe('exportStartedNotice', () => {
  it.each([
    [18_432, 'articles-2026-11-27.csv will download when it’s ready — 18,432 rows.'],
    [1, 'articles-2026-11-27.csv will download when it’s ready — 1 row.'],
    [0, 'articles-2026-11-27.csv will download when it’s ready — 0 rows.'],
  ])('with %i rows says “%s”', (rows, text) => {
    expect(exportStartedNotice('articles-2026-11-27.csv', rows)).toEqual({
      tone: 'info',
      title: 'Export started',
      text,
    });
  });
});

describe('exportTooLargeNotice', () => {
  it('asks staff to narrow the search or filters', () => {
    expect(exportTooLargeNotice()).toEqual({
      tone: 'warning',
      title: 'Too many articles to export',
      text: 'Exports stop at 50,000 rows. Narrow the search or filters, then export again.',
    });
  });
});

describe('exportFailedNotice', () => {
  it('says nothing was downloaded', () => {
    expect(exportFailedNotice()).toEqual({
      tone: 'danger',
      title: 'Keryx couldn’t export the articles',
      text: 'Nothing was downloaded.',
    });
  });
});
