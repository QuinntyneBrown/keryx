import { InjectionToken } from '@angular/core';

/** An article's status as the admin API serves it. */
export type ArticleStatus = 'published' | 'needs_review' | 'hidden';

/** A row of `GET /admin/v1/articles`. Its text came from a source: render it as text. */
export interface ArticleRow {
  id: string;
  title: string | null;
  organization: string | null;
  category: string;
  region: string;
  relevance: number;
  status: ArticleStatus;
  published_at: string | null;
  collected_at: string;
}

/** One page of the list, newest collected first; every count is a number. */
export interface ArticlePage {
  items: ArticleRow[];
  total: number;
  page: number;
  page_size: number;
}

/** The whole total of each status, whatever the search (L2-049). */
export interface ArticleCounts {
  all: number;
  needs_review: number;
  published: number;
  hidden: number;
}

/** Where an article was read: the source (null once removed), its name, the URL, first and last seen. */
export interface ArticleProvenance {
  source_id: string | null;
  source_name: string;
  url: string;
  first_seen_at: string;
  last_seen_at: string;
}

/** One article with its provenance, `GET /admin/v1/articles/{id}`. */
export interface ArticleDetail extends ArticleRow {
  status_reason: string | null;
  threshold: number;
  decided_by: 'system' | 'staff';
  summary: string;
  url: string;
  source: { id: string; name: string } | null;
  tags: string[];
  provenance: ArticleProvenance[];
}

/** What the list asks for: a term of fewer than 2 characters is never sent. */
export interface ArticleListQuery {
  page: number;
  pageSize: number;
  status?: ArticleStatus;
  q?: string;
  region?: string;
  category?: string;
}

/** The admin articles (docs/detailed-designs/admin-app/review-articles/). It rejects with an `ApiProblem`. */
export interface ArticlesApi {
  list(query: ArticleListQuery, signal: AbortSignal): Promise<ArticlePage>;
  counts(signal: AbortSignal): Promise<ArticleCounts>;
  /** One article; an unknown id rejects with code `article_not_found`. */
  get(id: string, signal: AbortSignal): Promise<ArticleDetail>;
}

export const ARTICLES_API = new InjectionToken<ArticlesApi>('Keryx articles');
