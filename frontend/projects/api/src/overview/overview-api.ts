import { InjectionToken } from '@angular/core';

/** How a run ended, or `running`. */
export type RunOutcome = 'running' | 'succeeded' | 'completed_with_issues' | 'failed';

/** A collection run as Overview shows it; every count a number. */
export interface OverviewRun {
  id: string;
  started_at: string;
  finished_at: string | null;
  duration_ms: number | null;
  outcome: RunOutcome;
  /** Null unless it failed: `store_failed` and other `<stage>_failed`, `interrupted`, `unexpected`. */
  failure_code: string | null;
  added: number;
  sources_ok: number;
  sources_checked: number;
  sources_total: number;
}

/** The top of Overview: the last and current runs, what exists, and the next run. */
export interface OverviewStatus {
  last_run: OverviewRun | null;
  current_run: OverviewRun | null;
  has_sources: boolean;
  has_runs: boolean;
  next_run_at: string;
  time_zone: string;
}

/** The shell's schedule: the next collection, the interval, the zone and the failing sources. */
export interface ShellSchedule {
  next_run_at: string;
  interval_hours: number;
  time_zone: string;
  failing_sources: number;
}

/** Overview's reads (L2-043–L2-046) and the shell's schedule (L2-042). */
export interface OverviewApi {
  status(signal: AbortSignal): Promise<OverviewStatus>;
  schedule(signal: AbortSignal): Promise<ShellSchedule>;
}

export const OVERVIEW_API = new InjectionToken<OverviewApi>('Keryx overview');
