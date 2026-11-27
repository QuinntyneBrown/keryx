import {
  articleQueryFromParams,
  articleQueryToParams,
  changeArticleQuery,
  searchTerm,
} from '../public-api';

describe('articleQueryFromParams', () => {
  it('reads a full URL query', () => {
    expect(
      articleQueryFromParams({
        status: 'needs_review',
        q: 'pickleball',
        region: 'toronto',
        category: 'events',
        page: '3',
        article: 'art_aaaaaaaaaaaa',
      }),
    ).toEqual({
      status: 'needs_review',
      q: 'pickleball',
      region: 'toronto',
      category: 'events',
      page: 3,
      article: 'art_aaaaaaaaaaaa',
    });
  });

  it.each([
    [{}, { status: null, q: '', region: null, category: null, page: 1, article: null }],
    [
      { status: 'draft' },
      { status: null, q: '', region: null, category: null, page: 1, article: null },
    ],
    [{ page: '0' }, { status: null, q: '', region: null, category: null, page: 1, article: null }],
    [{ page: '-2' }, { status: null, q: '', region: null, category: null, page: 1, article: null }],
    [
      { page: '2.5' },
      { status: null, q: '', region: null, category: null, page: 1, article: null },
    ],
    [
      { page: 'ten' },
      { status: null, q: '', region: null, category: null, page: 1, article: null },
    ],
    [
      { region: '', category: '' },
      { status: null, q: '', region: null, category: null, page: 1, article: null },
    ],
  ])('reads %o as %o', (params, expected) => {
    expect(articleQueryFromParams(params)).toEqual(expected);
  });
});

describe('articleQueryToParams', () => {
  it('writes only what differs from the defaults, and null removes the rest from the URL', () => {
    expect(
      articleQueryToParams({
        status: null,
        q: '',
        region: null,
        category: null,
        page: 1,
        article: null,
      }),
    ).toEqual({ status: null, q: null, region: null, category: null, page: null, article: null });
    expect(
      articleQueryToParams({
        status: 'hidden',
        q: 'run',
        region: 'gta',
        category: 'nutrition',
        page: 2,
        article: 'art_bbbbbbbbbbbb',
      }),
    ).toEqual({
      status: 'hidden',
      q: 'run',
      region: 'gta',
      category: 'nutrition',
      page: '2',
      article: 'art_bbbbbbbbbbbb',
    });
  });
});

describe('changeArticleQuery', () => {
  const on3 = {
    status: null,
    q: 'run',
    region: null,
    category: null,
    page: 3,
    article: null,
  } as const;

  // The page returns to 1 when the tab, the search or a filter changes (L2-050 AC3).
  it.each([
    [{ status: 'published' as const }, 1],
    [{ q: 'walk' }, 1],
    [{ region: 'toronto' }, 1],
    [{ category: 'events' }, 1],
    [{ page: 4 }, 4],
    [{ article: 'art_cccccccccccc' }, 3],
  ])('after %o is on page %i', (change, page) => {
    expect(changeArticleQuery(on3, change).page).toBe(page);
  });

  it('keeps the page when a change repeats the current value', () => {
    expect(changeArticleQuery(on3, { q: 'run' }).page).toBe(3);
  });
});

describe('searchTerm', () => {
  // A term of 1 character sends no search (L2-050 AC2).
  it.each([
    ['', null],
    ['   ', null],
    ['p', null],
    [' p ', null],
    ['pi', 'pi'],
    ['  pickleball ', 'pickleball'],
  ])('sends %j as %j', (q, expected) => {
    expect(searchTerm(q)).toBe(expected);
  });
});
