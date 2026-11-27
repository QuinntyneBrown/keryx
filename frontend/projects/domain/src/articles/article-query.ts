import type { PublicationStatus } from './publication-status';

/**
 * The Articles screen's list, as its URL names it (L2-050 AC6): the status tab, the search, the
 * region and category filters, the page and the open article.
 */
export interface ArticleQuery {
  status: PublicationStatus | null;
  q: string;
  region: string | null;
  category: string | null;
  page: number;
  article: string | null;
}

/** The URL query's values, as the router gives them. */
export type ArticleParams = Partial<Record<keyof ArticleQuery, string | undefined>>;

/** The URL query to merge: null removes a value, so a default never shows in the URL. */
export type ArticleQueryParams = Record<keyof ArticleQuery, string | null>;

const STATUSES: readonly string[] = ['published', 'needs_review', 'hidden'];

/** Reads the URL query; a value it cannot use reads as its default. */
export function articleQueryFromParams(params: ArticleParams): ArticleQuery {
  const page = /^\d+$/.test(params.page ?? '') ? Number(params.page) : 1;
  return {
    status: STATUSES.includes(params.status ?? '') ? (params.status as PublicationStatus) : null,
    q: params.q ?? '',
    region: params.region || null,
    category: params.category || null,
    page: page >= 1 ? page : 1,
    article: params.article || null,
  };
}

/** Writes the URL query: page 1, an empty search and no filter leave the URL. */
export function articleQueryToParams(query: ArticleQuery): ArticleQueryParams {
  return {
    status: query.status,
    q: query.q || null,
    region: query.region,
    category: query.category,
    page: query.page > 1 ? String(query.page) : null,
    article: query.article,
  };
}

/** Applies a change; changing the tab, the search or a filter returns to page 1 (L2-050 AC3). */
export function changeArticleQuery(
  query: ArticleQuery,
  change: Partial<ArticleQuery>,
): ArticleQuery {
  const next = { ...query, ...change };
  const narrowed = (['status', 'q', 'region', 'category'] as const).some(
    (key) => next[key] !== query[key],
  );
  return narrowed && change.page === undefined ? { ...next, page: 1 } : next;
}

/** The term to send: null for an empty or 1-character search, which sends none (L2-050 AC2). */
export function searchTerm(q: string): string | null {
  const term = q.trim();
  return term.length >= 2 ? term : null;
}
