import { InjectionToken } from '@angular/core';
import type { Region } from '../sources/sources-api';

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
/** An article's status after a decision, and the status the decision replaced, for Undo. */
export interface ArticleDecision {
  id: string;
  status: ArticleStatus;
  decided_by: 'system' | 'staff';
  previous_status: ArticleStatus | null;
}

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
  /** The regions in rank order, which name each row's region and fill the Region filter. */
  regions(signal: AbortSignal): Promise<Region[]>;
  /** Publishes the article whatever its score (L2-014). */
  publish(id: string, signal: AbortSignal): Promise<ArticleDecision>;
  /** Hides the article from the consumer API whatever its score (L2-014). */
  hide(id: string, signal: AbortSignal): Promise<ArticleDecision>;
  /** Undoes this session's decision, naming the status it replaced; `revert_unavailable` once it can't. */
  revert(id: string, previousStatus: ArticleStatus, signal: AbortSignal): Promise<ArticleDecision>;
}

export const ARTICLES_API = new InjectionToken<ArticlesApi>('Keryx articles');
