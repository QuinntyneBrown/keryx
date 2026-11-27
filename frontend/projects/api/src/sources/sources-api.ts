import { InjectionToken } from '@angular/core';

/** The kinds of source (L2-001). Staff add every kind but `news_query`, which seed data creates. */
export type SourceKind = 'rss' | 'website' | 'instagram' | 'eventbrite' | 'news_query';

/** A source's health, derived by the runs (L2-062). */
export type SourceHealth = 'healthy' | 'needs_look' | 'failing';

/** A row of `GET /admin/v1/sources`. Every count is a number. */
export interface SourceListItem {
  id: string;
  kind: SourceKind;
  address: string;
  organization_name: string;
  region: string;
  region_name: string;
  health: SourceHealth;
  failure_reason: string | null;
  consecutive_failures: number;
  held_count: number;
  articles_30d: number;
  last_fetched_at: string | null;
}

/** One page of the catalogue, with counts across all of it whatever the filter. */
export interface SourcePage {
  items: SourceListItem[];
  total: number;
  page: number;
  page_size: number;
  all_count: number;
  failing_count: number;
  needs_look_count: number;
}

export interface SourceQuery {
  page: number;
  pageSize: number;
  kind?: SourceKind;
}

/** The source catalogue (docs/detailed-designs/sources/). It rejects with an `ApiProblem`. */
export interface SourcesApi {
  list(query: SourceQuery, signal: AbortSignal): Promise<SourcePage>;
}

export const SOURCES_API = new InjectionToken<SourcesApi>('Keryx sources');
