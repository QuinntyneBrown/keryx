import { InjectionToken } from '@angular/core';
import type { RunOutcome } from '../overview/overview-api';

/** A run in the runs list; every count a number. */
export interface RunSummary {
  id: string;
  trigger: string;
  started_at: string;
  finished_at: string | null;
  duration_ms: number | null;
  outcome: RunOutcome;
  added: number;
  sources_failed: number;
}

/** A page of runs, newest first, with the total that matched. */
export interface RunPage {
  items: RunSummary[];
  total: number;
  page: number;
  page_size: number;
}

/** Which runs to list: one page of the collection worker's, at most 50 a page. */
export interface RunQuery {
  page: number;
  pageSize: number;
}

/** The run history reads (View run history design, L2-044, L2-056). */
export interface RunsApi {
  list(query: RunQuery, signal: AbortSignal): Promise<RunPage>;
}

export const RUNS_API = new InjectionToken<RunsApi>('Keryx runs');
