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
  /** How many of the run's sources it has checked so far, of how many (L2-058 AC1). */
  sources_checked: number;
  sources_total: number;
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
  /** Only runs that failed, completed with issues or had a failed source. */
  problems?: boolean;
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

/** One pipeline stage of a run: its status, how long it took, its named counts, a generic reason. */
export interface StageResult {
  stage: string;
  status: string;
  duration_ms: number;
  counts: Record<string, number>;
  reason: string | null;
}

/** A problem a run raised. Its names and detail came from a source: render them as text. */
export interface RunIssue {
  id: string;
  severity: 'error' | 'warning';
  kind: string;
  subject_type: string;
  subject_id: string | null;
  subject_name: string | null;
  detail: string;
  consecutive_runs: number;
  since: string;
  muted: boolean;
}

/** A run as staff read it (L2-019): every count a number, its six stages and its issues. */
export interface RunDetail extends RunSummary {
  worker: string;
  failure_reason: string | null;
  correlation_id: string | null;
  updated: number;
  unchanged: number;
  duplicates: number;
  held: number;
  off_topic: number;
  rejected: number;
  sources_ok: number;
  sources_checked: number;
  sources_total: number;
  stages: StageResult[];
  issues: RunIssue[];
}

/** The run history reads (View run history design, L2-044, L2-045, L2-056, L2-057). */
export interface RunsApi {
  list(query: RunQuery, signal: AbortSignal): Promise<RunPage>;
  /** One run with its stages and issues; refuses `run_not_found`. */
  get(id: string, signal: AbortSignal): Promise<RunDetail>;
  /** The `pageSize` most severe open issues: errors first, then the longest streak. */
  openIssues(pageSize: number, signal: AbortSignal): Promise<OpenIssuePage>;
}

export const RUNS_API = new InjectionToken<RunsApi>('Keryx runs');
