import { InjectionToken } from '@angular/core';
import type { NewSource, SourceChanges, SourceDetail, SourceSaved } from './new-source';

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

/** Why a test fetch could not read an address (L2-002 AC2). */
export type FetchFailure =
  'not_found' | 'timeout' | 'blocked_by_robots' | 'refused' | 'invalid_content' | 'unsafe_address';

/** What one test fetch found; it stores nothing (L2-002). */
export interface SourceTestResult {
  reachable: boolean;
  reason: FetchFailure | null;
  items_found: number;
  items_relevant: number;
}

/** A region staff choose from, Toronto first (L2-018). */
export interface Region {
  slug: string;
  name: string;
  time_zone: string;
}

export interface SourceQuery {
  page: number;
  pageSize: number;
  kind?: SourceKind;
}

/** The source catalogue (docs/detailed-designs/sources/). It rejects with an `ApiProblem`. */
export interface SourcesApi {
  list(query: SourceQuery, signal: AbortSignal): Promise<SourcePage>;
  /** The regions in rank order. */
  regions(signal: AbortSignal): Promise<Region[]>;
  /** Test-fetches an address before it is added. An unreachable one resolves, with a reason. */
  testAddress(kind: SourceKind, address: string, signal: AbortSignal): Promise<SourceTestResult>;
  /**
   * Test-fetches a source in the catalogue, leaving its health as it is (L2-063 AC3); refuses
   * `source_not_found` when it was removed meanwhile.
   */
  testSource(id: string, signal: AbortSignal): Promise<SourceTestResult>;
  /** Adds a source; a refusal names its `code` (`source_exists`, `unsafe_address`, …). */
  create(source: NewSource, signal: AbortSignal): Promise<SourceDetail>;
  /**
   * Saves "Edit source" and tests the saved address (L2-065 AC2); refuses `source_exists`,
   * `validation_failed` or `source_not_found`.
   */
  update(id: string, changes: SourceChanges, signal: AbortSignal): Promise<SourceSaved>;
  /** One source, with its article count; refuses `source_not_found`. */
  source(id: string, signal: AbortSignal): Promise<SourceDetail>;
  /** Removes a source: it is no longer fetched or listed, and its articles stay (L2-065 AC4). */
  remove(id: string, signal: AbortSignal): Promise<void>;
  /** The Undo of a removal; refuses `source_exists` when it was added again meanwhile. */
  restore(id: string, signal: AbortSignal): Promise<SourceDetail>;
}

export const SOURCES_API = new InjectionToken<SourcesApi>('Keryx sources');
