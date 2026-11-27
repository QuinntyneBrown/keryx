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

/** An issue still open, as last seen in a run. Its names came from a source: render them as text. */
export interface OpenIssue {
  id: string;
  /** The run it last occurred in. */
  run_id: string;
  severity: 'error' | 'warning';
  kind: string;
  subject_type: string;
  subject_id: string | null;
  subject_name: string | null;
  detail: string;
  consecutive_runs: number;
  since: string;
}

/** The most severe open issues, with how many are open in all. */
export interface OpenIssuePage {
  items: OpenIssue[];
  total: number;
  page_size: number;
}

/** The run history reads (View run history design, L2-044, L2-045, L2-056). */
export interface RunsApi {
  list(query: RunQuery, signal: AbortSignal): Promise<RunPage>;
  /** The `pageSize` most severe open issues: errors first, then the longest streak. */
  openIssues(pageSize: number, signal: AbortSignal): Promise<OpenIssuePage>;
}

export const RUNS_API = new InjectionToken<RunsApi>('Keryx runs');
