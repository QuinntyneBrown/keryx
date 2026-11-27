/** 28 November 2026, 9:46 AM in Toronto; the 6-hourly run at 6:00 AM was the last. */
export const OVERVIEW_NOW = new Date('2026-11-28T14:46:00Z');

/** A collection run as Overview's status serves it; the mock's run_2418 by default. */
export function overviewRun(fields: Record<string, unknown> = {}) {
  return {
    id: 'run_2418',
    started_at: '2026-11-28T11:00:00Z',
    finished_at: '2026-11-28T11:04:12Z',
    duration_ms: 252_000,
    outcome: 'completed_with_issues',
    failure_code: null,
    added: 47,
    sources_ok: 62,
    sources_checked: 64,
    sources_total: 64,
    ...fields,
  };
}

/** Overview's status: the last run of the mocks, none in progress, and the next run at 12:00 PM. */
export function overviewStatus(fields: Record<string, unknown> = {}) {
  return {
    last_run: overviewRun(),
    current_run: null,
    has_sources: true,
    has_runs: true,
    next_run_at: '2026-11-28T17:00:00Z',
    time_zone: 'America/Toronto',
    ...fields,
  };
}
