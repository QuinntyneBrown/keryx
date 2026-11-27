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

/** A run as the runs list serves it (View run history design). */
export function runSummary(
  id: string,
  started_at: string,
  outcome: string,
  added: number,
): Record<string, unknown> {
  return {
    id,
    trigger: 'scheduled',
    started_at,
    finished_at: outcome === 'running' ? null : started_at,
    duration_ms: outcome === 'running' ? null : 240_000,
    outcome,
    added,
    sources_failed: outcome === 'completed_with_issues' ? 2 : 0,
  };
}

/**
 * The mock's last 14 runs, newest first as the API serves them: every six hours from Wednesday
 * 6:00 AM to today 6:00 AM in Toronto, a failed run yesterday at noon and a manual one at 2:37 PM.
 */
export const LAST_14_RUNS = [
  runSummary('run_2418', '2026-11-28T11:00:00Z', 'completed_with_issues', 47),
  runSummary('run_2417', '2026-11-28T05:00:00Z', 'succeeded', 12),
  runSummary('run_2416', '2026-11-27T23:00:00Z', 'succeeded', 31),
  runSummary('run_2415', '2026-11-27T19:37:00Z', 'succeeded', 8),
  runSummary('run_2414', '2026-11-27T17:00:00Z', 'failed', 0),
  runSummary('run_2413', '2026-11-27T11:00:00Z', 'succeeded', 39),
  runSummary('run_2412', '2026-11-27T05:00:00Z', 'succeeded', 6),
  runSummary('run_2411', '2026-11-26T23:00:00Z', 'succeeded', 28),
  runSummary('run_2410', '2026-11-26T17:00:00Z', 'completed_with_issues', 22),
  runSummary('run_2409', '2026-11-26T11:00:00Z', 'succeeded', 41),
  runSummary('run_2408', '2026-11-26T05:00:00Z', 'succeeded', 9),
  runSummary('run_2407', '2026-11-25T23:00:00Z', 'succeeded', 35),
  runSummary('run_2406', '2026-11-25T17:00:00Z', 'succeeded', 26),
  runSummary('run_2405', '2026-11-25T11:00:00Z', 'succeeded', 19),
];

/** A page of the runs list. */
export function runPage(items: readonly Record<string, unknown>[], total = items.length) {
  return { items, total, page: 1, page_size: 14 };
}
